using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Course lifecycle: duration-based auto-removal of students, the professor
/// emails around it, and the course-page actions (pause, hold 7 days, "not in
/// use", remove). Rules live in <see cref="CourseLifecycle"/>.
///
/// The daily job only emails the professors ASSIGNED to the course; higher
/// roles can act on the course page but are never emailed.
/// </summary>
public class CourseLifecycleService : ICourseLifecycleService
{
    private const string SystemUsername = "system:course-lifecycle";

    private readonly ICourseLifecycleRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;
    private readonly TimeProvider _clock;
    private readonly ILogger<CourseLifecycleService> _logger;

    public CourseLifecycleService(
        ICourseLifecycleRepository repository,
        IEmailService emailService,
        IAuditLogService auditLogService,
        TimeProvider clock,
        ILogger<CourseLifecycleService> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _auditLogService = auditLogService;
        _clock = clock;
        _logger = logger;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    // ────────────────────────────────────────────────────────────────
    //  Daily job
    // ────────────────────────────────────────────────────────────────

    public async Task RunDailyAsync()
    {
        var courses = await _repository.GetActiveInstitutionCoursesAsync();
        _logger.LogInformation("Course lifecycle job: {Count} active courses", courses.Count);

        foreach (var course in courses)
        {
            try
            {
                await ProcessCourseAsync(course, UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Course lifecycle failed for course {CourseId}", course.Id);
            }
        }
    }

    private async Task ProcessCourseAsync(Course course, DateTime now)
    {
        var studentsCount = await _repository.CountEnrolledStudentsAsync(course.Id);

        if (studentsCount == 0)
        {
            await SendUnusedAlertIfDueAsync(course, now);
        }

        if (course.DurationMonths is null) return;

        // Start the clock for this cohort the first time we see students.
        if (course.LifecycleStartedAt is null)
        {
            if (studentsCount == 0) return;
            course.LifecycleStartedAt = CourseLifecycle.ClockStart(course, await _repository.GetEarliestEnrollmentAsync(course.Id));
            await _repository.SaveCourseAsync(course);
        }

        if (course.AutoRemovalPaused) return;

        var scheduled = CourseLifecycle.ScheduledRemovalAt(course, course.LifecycleStartedAt)!.Value;
        if (now < scheduled - CourseLifecycle.WarningNotice) return;

        // Everyone already left (removed by hand): nothing to warn about or remove —
        // just wait for the next cohort.
        if (studentsCount == 0)
        {
            course.LifecycleStartedAt = null;
            course.AutoRemovalExtensionDays = 0;
            await _repository.SaveCourseAsync(course);
            return;
        }

        // 1) The 7-day warning (once per scheduled date — a hold creates a new date and a new warning).
        var warning = await _repository.GetNotificationAsync(course.Id, CourseLifecycle.WarningKey(scheduled));
        if (warning is null)
        {
            var removalAt = CourseLifecycle.EffectiveRemovalAt(scheduled, now);
            var sent = await EmailProfessorsAsync(course, (prof, lang) => _emailService.SendCourseRemovalWarningEmailAsync(
                prof.Email, prof.FirstName, course.Id, course.Name, studentsCount, FormatDate(removalAt, lang), lang));
            await _repository.AddNotificationAsync(new CourseLifecycleNotification
            {
                CourseId = course.Id,
                Key = CourseLifecycle.WarningKey(scheduled),
                SentAt = now,
                RecipientsCount = sent,
            });
            _logger.LogInformation("Course {CourseId}: removal warning sent to {Sent} professor(s), removal on {RemovalAt:yyyy-MM-dd}",
                course.Id, sent, removalAt);
            return;
        }

        // 2) Removal — never sooner than 7 days after the warning actually went out.
        var effective = CourseLifecycle.EffectiveRemovalAt(scheduled, warning.SentAt);
        if (now + CourseLifecycle.JobJitter < effective) return;

        var removed = await _repository.RemoveAllStudentsAsync(course.Id);
        course.StudentsRemovedAt = now;
        course.LifecycleStartedAt = null;
        course.AutoRemovalExtensionDays = 0;
        await _repository.SaveCourseAsync(course);
        await _repository.AddNotificationAsync(new CourseLifecycleNotification
        {
            CourseId = course.Id,
            Key = CourseLifecycle.RemovedKey(scheduled),
            SentAt = now,
            RecipientsCount = 0,
        });
        await _auditLogService.LogAsync(0, SystemUsername, course.UniversityId, "AutoRemoveStudents", "Course",
            course.Id, course.Name, new Dictionary<string, (object? OldValue, object? NewValue)>
            {
                ["Students"] = (removed, 0),
            });
        _logger.LogInformation("Course {CourseId}: duration ended — {Removed} student enrollment(s) removed", course.Id, removed);
    }

    private async Task SendUnusedAlertIfDueAsync(Course course, DateTime now)
    {
        // Empty for 3+ days: the access key has existed that long, and so has the
        // emptiness after the last auto-removal.
        var oldestKey = await _repository.GetOldestActiveKeyCreatedAtAsync(course.Id, now);
        if (oldestKey is null || oldestKey > now - CourseLifecycle.UnusedAfter) return;
        if (course.StudentsRemovedAt is { } removedAt && removedAt > now - CourseLifecycle.UnusedAfter) return;

        var key = CourseLifecycle.UnusedKey(now);
        if (await _repository.GetNotificationAsync(course.Id, key) is not null) return;

        var sent = await EmailProfessorsAsync(course, (prof, lang) =>
            _emailService.SendCourseUnusedAlertEmailAsync(prof.Email, prof.FirstName, course.Id, course.Name, lang));
        await _repository.AddNotificationAsync(new CourseLifecycleNotification
        {
            CourseId = course.Id,
            Key = key,
            SentAt = now,
            RecipientsCount = sent,
        });
        _logger.LogInformation("Course {CourseId}: unused-course alert sent to {Sent} professor(s)", course.Id, sent);
    }

    private async Task<int> EmailProfessorsAsync(Course course, Func<User, string, Task> send)
    {
        var professors = await _repository.GetAssignedProfessorsAsync(course.Id);
        var sent = 0;
        foreach (var professor in professors)
        {
            if (string.IsNullOrWhiteSpace(professor.Email)) continue;
            var language = string.IsNullOrWhiteSpace(professor.LanguagePreference) ? "pt-br" : professor.LanguagePreference;
            try
            {
                await send(professor, language);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lifecycle email to {Email} failed for course {CourseId}", professor.Email, course.Id);
            }
        }
        return sent;
    }

    // ────────────────────────────────────────────────────────────────
    //  Course page
    // ────────────────────────────────────────────────────────────────

    public async Task<CourseLifecycleInfo> GetAsync(int courseId, User caller)
    {
        var course = await LoadVisibleAsync(courseId, caller);
        return await BuildInfoAsync(course, caller);
    }

    public async Task<CourseLifecycleInfo> PauseAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.DurationMonths is null)
            throw new InvalidOperationException("This course has no duration, so there is nothing to pause.");
        if (!course.AutoRemovalPaused)
        {
            course.AutoRemovalPaused = true;
            await SaveAndAuditAsync(course, caller, "PauseAutoRemoval", "AutoRemovalPaused", false, true);
        }
        return await BuildInfoAsync(course, caller);
    }

    public async Task<CourseLifecycleInfo> ResumeAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.AutoRemovalPaused)
        {
            course.AutoRemovalPaused = false;
            await GuaranteeNoticeAsync(course);
            await SaveAndAuditAsync(course, caller, "ResumeAutoRemoval", "AutoRemovalPaused", true, false);
        }
        return await BuildInfoAsync(course, caller);
    }

    public async Task<CourseLifecycleInfo> ExtendAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.DurationMonths is null)
            throw new InvalidOperationException("This course has no duration, so there is nothing to hold.");
        if (course.LifecycleStartedAt is null && await _repository.CountEnrolledStudentsAsync(course.Id) == 0)
            throw new InvalidOperationException("The clock hasn't started yet — there are no students in this course.");

        var before = course.AutoRemovalExtensionDays;
        course.AutoRemovalExtensionDays += CourseLifecycle.ExtensionDays;
        await SaveAndAuditAsync(course, caller, "ExtendAutoRemoval", "AutoRemovalExtensionDays", before, course.AutoRemovalExtensionDays);
        return await BuildInfoAsync(course, caller);
    }

    public async Task<CourseLifecycleInfo> DeactivateAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.DeactivatedAt is null)
        {
            course.DeactivatedAt = UtcNow;
            await SaveAndAuditAsync(course, caller, "Deactivate", "DeactivatedAt", null, course.DeactivatedAt);
        }
        return await BuildInfoAsync(course, caller);
    }

    public async Task<CourseLifecycleInfo> ReactivateAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.DeactivatedAt is { } was)
        {
            course.DeactivatedAt = null;
            await GuaranteeNoticeAsync(course);
            await SaveAndAuditAsync(course, caller, "Reactivate", "DeactivatedAt", was, null);
        }
        return await BuildInfoAsync(course, caller);
    }

    public async Task DeleteAsync(int courseId, User caller)
    {
        var course = await LoadManageableAsync(courseId, caller);
        if (course.DeactivatedAt is null)
            throw new InvalidOperationException("Mark the course as not in use before removing it from the list.");

        course.DeletedAt = UtcNow;
        await SaveAndAuditAsync(course, caller, "SoftDelete", "DeletedAt", null, course.DeletedAt);
    }

    /// <summary>After a pause or deactivation, removal is always at least 7 days out (fresh warning).</summary>
    private async Task GuaranteeNoticeAsync(Course course)
    {
        if (course.DurationMonths is null) return;
        var start = CourseLifecycle.ClockStart(course, await _repository.GetEarliestEnrollmentAsync(course.Id));
        var extra = CourseLifecycle.DaysToGuaranteeNotice(CourseLifecycle.ScheduledRemovalAt(course, start), UtcNow);
        course.AutoRemovalExtensionDays += extra;
    }

    private async Task SaveAndAuditAsync(Course course, User caller, string action, string field, object? oldValue, object? newValue)
    {
        await _repository.SaveCourseAsync(course);
        await _auditLogService.LogAsync(caller.UserId, caller.Username, course.UniversityId, action, "Course",
            course.Id, course.Name, new Dictionary<string, (object? OldValue, object? NewValue)>
            {
                [field] = (oldValue, newValue),
            });
    }

    private async Task<CourseLifecycleInfo> BuildInfoAsync(Course course, User caller)
    {
        var now = UtcNow;
        var studentsCount = await _repository.CountEnrolledStudentsAsync(course.Id);
        var oldestKey = await _repository.GetOldestActiveKeyCreatedAtAsync(course.Id, now);
        var start = CourseLifecycle.ClockStart(course,
            studentsCount > 0 ? await _repository.GetEarliestEnrollmentAsync(course.Id) : null);

        var info = new CourseLifecycleInfo
        {
            CourseId = course.Id,
            DurationMonths = course.DurationMonths,
            StartedAt = start,
            ExpiresAt = CourseLifecycle.ExpiresAt(course, start),
            ExtensionDays = course.AutoRemovalExtensionDays,
            Paused = course.AutoRemovalPaused,
            DeactivatedAt = course.DeactivatedAt,
            StudentsCount = studentsCount,
            HasActiveKey = oldestKey is not null,
            UnusedAlertActive = course.DeactivatedAt is null
                && studentsCount == 0
                && oldestKey is { } k && k <= now - CourseLifecycle.UnusedAfter
                && !(course.StudentsRemovedAt is { } r && r > now - CourseLifecycle.UnusedAfter),
            CanManage = await CanManageAsync(course, caller),
        };

        if (CourseLifecycle.ScheduledRemovalAt(course, start) is { } scheduled)
        {
            var warning = await _repository.GetNotificationAsync(course.Id, CourseLifecycle.WarningKey(scheduled));
            info.WarningSentAt = warning?.SentAt;
            info.RemovalAt = CourseLifecycle.EffectiveRemovalAt(scheduled, warning?.SentAt);
        }

        info.Status = course.DeactivatedAt is not null ? "inactive"
            : course.DurationMonths is null ? "no_duration"
            : start is null ? "waiting_students"
            : course.AutoRemovalPaused ? "paused"
            : info.RemovalAt is { } removal && now >= removal - CourseLifecycle.WarningNotice ? "removal_scheduled"
            : "running";

        return info;
    }

    // ────────────────────────────────────────────────────────────────
    //  Access
    // ────────────────────────────────────────────────────────────────

    private async Task<Course> LoadVisibleAsync(int courseId, User caller)
    {
        var course = await _repository.GetCourseAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found");
        // Tenant isolation: other universities' courses don't exist for the caller.
        if (caller.UserType != UserTypes.SuperAdmin && caller.UniversityId != course.UniversityId)
            throw new KeyNotFoundException("Course not found");
        return course;
    }

    private async Task<Course> LoadManageableAsync(int courseId, User caller)
    {
        var course = await LoadVisibleAsync(courseId, caller);
        if (!await CanManageAsync(course, caller))
            throw new UnauthorizedAccessException("Only the course's professors or higher roles can do this.");
        return course;
    }

    /// <summary>The assigned professor, or any role above professor in the course's university.</summary>
    private async Task<bool> CanManageAsync(Course course, User caller)
    {
        if (caller.UserType == UserTypes.SuperAdmin) return true;
        if (caller.UniversityId != course.UniversityId) return false;
        return caller.UserType switch
        {
            UserTypes.Manager or UserTypes.Tutor or UserTypes.PlatformCoordinator => true,
            UserTypes.Professor => (caller.IsAdmin ?? false)
                || await _repository.IsProfessorAssignedAsync(course.Id, caller.UserId),
            _ => false,
        };
    }

    // ────────────────────────────────────────────────────────────────

    private static string FormatDate(DateTime utc, string language)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SaoPaulo.Value);
        return language.ToLower() == "en" ? local.ToString("MM/dd/yyyy") : local.ToString("dd/MM/yyyy");
    }

    private static readonly Lazy<TimeZoneInfo> SaoPaulo = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    });
}
