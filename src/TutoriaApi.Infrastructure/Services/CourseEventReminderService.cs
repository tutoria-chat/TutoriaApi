using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Hourly Hangfire job: emails enrolled students about upcoming course events
/// (tests, assignment due dates, etc.) at the slots the professor selected
/// (7d / 3d / 2d / 24h before the event).
///
/// Timezone: events are stored in UTC; email copy formats them in
/// America/Sao_Paulo so an 8am Brazil test never reads as 5am.
/// </summary>
public class CourseEventReminderService : ICourseEventReminderService
{
    private readonly ICourseEventRepository _courseEventRepository;
    private readonly IStudentCourseRepository _studentCourseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<CourseEventReminderService> _logger;

    public CourseEventReminderService(
        ICourseEventRepository courseEventRepository,
        IStudentCourseRepository studentCourseRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<CourseEventReminderService> logger)
    {
        _courseEventRepository = courseEventRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    private sealed record Slot(string Key, TimeSpan Span, Func<CourseEvent, bool> IsEnabled);

    // Ordered smallest-first: the closest pending slot is the one that emails;
    // larger already-passed slots are logged as skipped so they never fire late.
    private static readonly Slot[] Slots =
    {
        new("24h", TimeSpan.FromHours(24), e => e.Remind24Hours),
        new("2d", TimeSpan.FromDays(2), e => e.Remind2Days),
        new("3d", TimeSpan.FromDays(3), e => e.Remind3Days),
        new("7d", TimeSpan.FromDays(7), e => e.Remind7Days),
    };

    public async Task ProcessRemindersAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var events = await _courseEventRepository.GetUpcomingWithRemindersAsync(nowUtc, nowUtc.AddDays(8));
        if (events.Count == 0) return;

        var saoPaulo = GetSaoPauloTimeZone();
        _logger.LogInformation("Reminder job: {Count} upcoming events with reminders enabled", events.Count);

        foreach (var courseEvent in events)
        {
            try
            {
                await ProcessEventAsync(courseEvent, nowUtc, saoPaulo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder processing failed for event {EventId}", courseEvent.Id);
            }
        }
    }

    private async Task ProcessEventAsync(CourseEvent courseEvent, DateTime nowUtc, TimeZoneInfo saoPaulo)
    {
        var timeLeft = courseEvent.StartsAtUtc - nowUtc;
        if (timeLeft <= TimeSpan.Zero) return;

        // All enabled slots whose window has been entered and that haven't fired yet
        var eligible = new List<Slot>();
        foreach (var slot in Slots)
        {
            if (!slot.IsEnabled(courseEvent)) continue;
            if (timeLeft > slot.Span) continue;
            if (await _courseEventRepository.HasReminderBeenSentAsync(courseEvent.Id, slot.Key)) continue;
            eligible.Add(slot);
        }
        if (eligible.Count == 0) return;

        // Smallest (closest) slot sends; larger eligible slots are marked skipped
        // so an event created at T-20h with 7d+24h checked emails exactly once.
        var sending = eligible[0];
        foreach (var skipped in eligible.Skip(1))
        {
            await _courseEventRepository.AddReminderLogAsync(new CourseEventReminderLog
            {
                CourseEventId = courseEvent.Id,
                ReminderKey = skipped.Key,
                SentAt = nowUtc,
                RecipientsCount = 0,
            });
        }

        var studentIds = await _studentCourseRepository.GetStudentIdsByCourseIdAsync(courseEvent.CourseId);
        var students = await _userRepository.GetActiveStudentsByIdsAsync(studentIds);

        var whenLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(courseEvent.StartsAtUtc, DateTimeKind.Utc), saoPaulo);

        var sent = 0;
        foreach (var student in students)
        {
            if (string.IsNullOrWhiteSpace(student.Email)) continue;
            var language = string.IsNullOrWhiteSpace(student.LanguagePreference) ? "pt-br" : student.LanguagePreference;
            try
            {
                await _emailService.SendCourseEventReminderEmailAsync(
                    toEmail: student.Email,
                    toName: student.FirstName,
                    eventTitle: courseEvent.Title,
                    eventType: courseEvent.EventType,
                    courseName: courseEvent.Course?.Name ?? string.Empty,
                    whenLocalFormatted: FormatWhen(whenLocal, language),
                    timeUntil: TimeUntilLabel(sending.Key, language),
                    languageCode: language);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send reminder to {Email} for event {EventId}",
                    student.Email, courseEvent.Id);
            }
        }

        await _courseEventRepository.AddReminderLogAsync(new CourseEventReminderLog
        {
            CourseEventId = courseEvent.Id,
            ReminderKey = sending.Key,
            SentAt = nowUtc,
            RecipientsCount = sent,
        });

        _logger.LogInformation(
            "Reminder '{Slot}' sent for event {EventId} ('{Title}') — {Sent}/{Total} students",
            sending.Key, courseEvent.Id, courseEvent.Title, sent, students.Count);
    }

    private static string FormatWhen(DateTime whenLocal, string language) => language.ToLower() switch
    {
        "en" => whenLocal.ToString("MM/dd/yyyy 'at' h:mm tt") + " (Brasília time)",
        "es" => whenLocal.ToString("dd/MM/yyyy 'a las' HH:mm") + " (hora de Brasilia)",
        _ => whenLocal.ToString("dd/MM/yyyy 'às' HH:mm") + " (horário de Brasília)",
    };

    private static string TimeUntilLabel(string slotKey, string language) => (slotKey, language.ToLower()) switch
    {
        ("24h", "en") => "tomorrow",
        ("24h", "es") => "mañana",
        ("24h", _) => "amanhã",
        ("2d", "en") => "in 2 days",
        ("2d", "es") => "en 2 días",
        ("2d", _) => "em 2 dias",
        ("3d", "en") => "in 3 days",
        ("3d", "es") => "en 3 días",
        ("3d", _) => "em 3 dias",
        (_, "en") => "in 7 days",
        (_, "es") => "en 7 días",
        _ => "em 7 dias",
    };

    private static TimeZoneInfo GetSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows fallback (local dev)
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
