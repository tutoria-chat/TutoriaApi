using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using TutoriaApi.Infrastructure.Services;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Course lifecycle on the EF in-memory provider with the real repository;
/// only email, audit and the clock are mocked.
/// </summary>
public class CourseLifecycleServiceTests : IDisposable
{
    // Course created and duration set on Oct 1 (12:00 UTC, the job's hour); first student Oct 3.
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FirstEnrollment = T0.AddDays(2);
    // 6 months from Oct 3 = Apr 3; +7 days leeway = Apr 10 removal; warning from Apr 3.
    private static readonly DateTime Expires = FirstEnrollment.AddMonths(6);
    private static readonly DateTime Removal = Expires.AddDays(7);

    private readonly TutoriaDbContext _db;
    private readonly FixedTimeProvider _clock = new(T0);
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly CourseLifecycleService _service;
    private readonly University _uni;
    private readonly Course _course;
    private readonly User _assignedProf;
    private readonly User _otherProf;
    private readonly User _manager;
    private readonly User _student;

    public CourseLifecycleServiceTests()
    {
        _db = new TutoriaDbContext(new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _uni = new University { Name = "Uni", Code = "UNI" };
        _db.Universities.Add(_uni);
        _db.SaveChanges();

        _course = new Course
        {
            Name = "Cálculo I", Code = "CALC1", UniversityId = _uni.Id,
            DurationMonths = 6, DurationSetAt = T0, CreatedAt = T0,
        };
        _db.Courses.Add(_course);
        _db.SaveChanges();

        _assignedProf = AddUser("prof", UserTypes.Professor, "prof@uni.br");
        _otherProf = AddUser("other", UserTypes.Professor, "other@uni.br");
        _manager = AddUser("manager", UserTypes.Manager, "manager@uni.br");
        _student = AddUser("aluno", UserTypes.Student, "aluno@uni.br");
        _db.ProfessorCourses.Add(new ProfessorCourse { ProfessorId = _assignedProf.UserId, CourseId = _course.Id });
        _db.StudentCourses.Add(new StudentCourse { StudentId = _student.UserId, CourseId = _course.Id, CreatedAt = FirstEnrollment });
        _db.SaveChanges();

        _service = new CourseLifecycleService(
            new CourseLifecycleRepository(_db), _email.Object, _audit.Object, _clock,
            Mock.Of<ILogger<CourseLifecycleService>>());
    }

    public void Dispose() => _db.Dispose();

    private User AddUser(string username, string type, string email, int? universityId = -1)
    {
        var user = new User
        {
            Username = username, Email = email, FirstName = username, LastName = "X",
            UserType = type, UniversityId = universityId == -1 ? _uni.Id : universityId,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private async Task RunAtAsync(DateTime utc)
    {
        _clock.Now = new DateTimeOffset(utc, TimeSpan.Zero);
        await _service.RunDailyAsync();
    }

    private Course Reload() => _db.Courses.AsNoTracking().Single(c => c.Id == _course.Id);
    private int Enrolled() => _db.StudentCourses.Count(sc => sc.CourseId == _course.Id);

    private void VerifyWarnings(Times times) => _email.Verify(e => e.SendCourseRemovalWarningEmailAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
        It.IsAny<string>(), It.IsAny<string>()), times);

    private void VerifyUnusedAlerts(Times times) => _email.Verify(e => e.SendCourseUnusedAlertEmailAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), times);

    // ── Rules ───────────────────────────────────────────────────────────────

    [Fact]
    public void ClockStart_NeverBeforeDurationWasSet()
    {
        var course = new Course { Name = "c", Code = "c", DurationMonths = 6, DurationSetAt = T0 };
        Assert.Equal(T0, CourseLifecycle.ClockStart(course, T0.AddYears(-1)));
        Assert.Equal(FirstEnrollment, CourseLifecycle.ClockStart(course, FirstEnrollment));
        Assert.Null(CourseLifecycle.ClockStart(course, null));
    }

    [Fact]
    public void ScheduledRemoval_IsDurationPlusLeewayPlusHolds()
    {
        var course = new Course { Name = "c", Code = "c", DurationMonths = 6, AutoRemovalExtensionDays = 14 };
        Assert.Equal(Removal.AddDays(14), CourseLifecycle.ScheduledRemovalAt(course, FirstEnrollment));
    }

    [Fact]
    public void EffectiveRemoval_LateWarningGetsFullNotice()
    {
        Assert.Equal(Removal, CourseLifecycle.EffectiveRemovalAt(Removal, Removal.AddDays(-7)));
        Assert.Equal(Removal.AddDays(3), CourseLifecycle.EffectiveRemovalAt(Removal, Removal.AddDays(-4)));
    }

    [Fact]
    public void DaysToGuaranteeNotice_PushesRemovalAtLeastSevenDaysOut()
    {
        Assert.Equal(0, CourseLifecycle.DaysToGuaranteeNotice(Removal, Removal.AddDays(-10)));
        Assert.Equal(9, CourseLifecycle.DaysToGuaranteeNotice(Removal, Removal.AddDays(2)));
        Assert.Equal(0, CourseLifecycle.DaysToGuaranteeNotice(null, Removal));
    }

    // ── Daily job: auto-removal ─────────────────────────────────────────────

    [Fact]
    public async Task RunDaily_FirstSeenStudents_StartsClockAtFirstEnrollment()
    {
        await RunAtAsync(T0.AddDays(5));

        Assert.Equal(FirstEnrollment, Reload().LifecycleStartedAt);
        VerifyWarnings(Times.Never());
    }

    [Fact]
    public async Task RunDaily_FullCycle_WarnsOnceThenRemovesStudentsAfterSevenDays()
    {
        await RunAtAsync(T0.AddDays(5));                 // clock starts
        await RunAtAsync(Expires.AddDays(-1));           // not yet
        VerifyWarnings(Times.Never());

        await RunAtAsync(Expires);                       // warning day
        await RunAtAsync(Expires.AddHours(1));           // same day again: no duplicate
        _email.Verify(e => e.SendCourseRemovalWarningEmailAsync(
            "prof@uni.br", "prof", _course.Id, "Cálculo I", 1, "10/04/2027", "pt-br"), Times.Once);
        VerifyWarnings(Times.Once());
        Assert.Equal(1, Enrolled());

        await RunAtAsync(Removal.AddDays(-1));           // still in leeway
        Assert.Equal(1, Enrolled());

        await RunAtAsync(Removal);                       // removal day
        Assert.Equal(0, Enrolled());
        Assert.Equal(Removal, (await _service.GetAsync(_course.Id, _assignedProf)).StudentsRemovedAt);
        var course = Reload();
        Assert.Null(course.LifecycleStartedAt);
        Assert.Equal(Removal, course.StudentsRemovedAt);
        Assert.Single(_db.Users.Where(u => u.UserId == _student.UserId)); // account kept
        _audit.Verify(a => a.LogAsync(0, It.IsAny<string>(), _uni.Id, "AutoRemoveStudents", "Course",
            _course.Id, "Cálculo I", It.IsAny<Dictionary<string, (object?, object?)>>()), Times.Once);
    }

    [Fact]
    public async Task RunDaily_WarningSentLate_RemovalWaitsSevenDaysAfterIt()
    {
        await RunAtAsync(T0.AddDays(5));
        await RunAtAsync(Removal.AddDays(2));            // job was down: warning only now

        VerifyWarnings(Times.Once());
        Assert.Equal(1, Enrolled());

        await RunAtAsync(Removal.AddDays(8));            // 6 days after the warning
        Assert.Equal(1, Enrolled());

        await RunAtAsync(Removal.AddDays(9));            // 7 days after the warning
        Assert.Equal(0, Enrolled());
    }

    [Fact]
    public async Task RunDaily_Paused_NeitherWarnsNorRemoves()
    {
        await RunAtAsync(T0.AddDays(5));
        await _service.PauseAsync(_course.Id, _assignedProf);

        await RunAtAsync(Expires);
        await RunAtAsync(Removal.AddDays(30));

        VerifyWarnings(Times.Never());
        Assert.Equal(1, Enrolled());
    }

    [Fact]
    public async Task Resume_AfterRemovalDate_GivesFreshSevenDayWarning()
    {
        await RunAtAsync(T0.AddDays(5));
        await RunAtAsync(Expires);                       // warned
        await _service.PauseAsync(_course.Id, _assignedProf);

        _clock.Now = new DateTimeOffset(Removal.AddDays(20), TimeSpan.Zero);
        var info = await _service.ResumeAsync(_course.Id, _assignedProf);
        Assert.True(info.RemovalAt >= Removal.AddDays(27));

        await RunAtAsync(Removal.AddDays(21));           // new warning, no removal
        VerifyWarnings(Times.Exactly(2));
        Assert.Equal(1, Enrolled());

        await RunAtAsync(Removal.AddDays(28));
        Assert.Equal(0, Enrolled());
    }

    [Fact]
    public async Task Extend_AfterWarning_HoldsSevenMoreDaysWithNewWarning()
    {
        await RunAtAsync(T0.AddDays(5));
        await RunAtAsync(Expires);                       // first warning
        _clock.Now = new DateTimeOffset(Expires.AddDays(2), TimeSpan.Zero);
        var info = await _service.ExtendAsync(_course.Id, _assignedProf);
        Assert.Equal(Removal.AddDays(7), info.RemovalAt);

        await RunAtAsync(Removal);                       // original date: second warning, nobody removed
        Assert.Equal(1, Enrolled());
        VerifyWarnings(Times.Exactly(2));

        await RunAtAsync(Removal.AddDays(7));
        Assert.Equal(0, Enrolled());
    }

    [Fact]
    public async Task RunDaily_StudentsAlreadyGone_ResetsSilently()
    {
        await RunAtAsync(T0.AddDays(5));
        _db.StudentCourses.RemoveRange(_db.StudentCourses);
        _db.SaveChanges();

        await RunAtAsync(Expires);

        VerifyWarnings(Times.Never());
        Assert.Null(Reload().LifecycleStartedAt);
    }

    [Fact]
    public async Task RunDaily_NoDuration_NeverRemoves()
    {
        _course.DurationMonths = null;
        _db.SaveChanges();

        await RunAtAsync(T0.AddYears(3));

        Assert.Equal(1, Enrolled());
        VerifyWarnings(Times.Never());
    }

    [Fact]
    public async Task RunDaily_OnlyAssignedProfessorsAreEmailed()
    {
        await RunAtAsync(T0.AddDays(5));
        await RunAtAsync(Expires);

        _email.Verify(e => e.SendCourseRemovalWarningEmailAsync("prof@uni.br", It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _email.Verify(e => e.SendCourseRemovalWarningEmailAsync(It.Is<string>(m => m != "prof@uni.br"), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── Daily job: unused-course alert ──────────────────────────────────────

    private void MakeEmptyCourseWithKey(DateTime keyCreatedAt)
    {
        _db.StudentCourses.RemoveRange(_db.StudentCourses);
        var module = new Module { Name = "M1", Code = "M1", SystemPrompt = "p", CourseId = _course.Id };
        _db.Modules.Add(module);
        _db.SaveChanges();
        _db.ModuleAccessTokens.Add(new ModuleAccessToken
        {
            Token = "tok", Name = "Moodle", ModuleId = module.Id, IsActive = true, CreatedAt = keyCreatedAt,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task UnusedAlert_KeyOlderThanThreeDaysAndNoStudents_EmailsDaily()
    {
        MakeEmptyCourseWithKey(T0);

        await RunAtAsync(T0.AddDays(2));                 // key only 2 days old
        VerifyUnusedAlerts(Times.Never());

        await RunAtAsync(T0.AddDays(3));
        await RunAtAsync(T0.AddDays(3).AddHours(2));     // same day: once
        await RunAtAsync(T0.AddDays(4));                 // next day: again
        _email.Verify(e => e.SendCourseUnusedAlertEmailAsync("prof@uni.br", "prof", _course.Id, "Cálculo I", "pt-br"),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UnusedAlert_NoKey_NoAlert()
    {
        _db.StudentCourses.RemoveRange(_db.StudentCourses);
        _db.SaveChanges();

        await RunAtAsync(T0.AddDays(10));

        VerifyUnusedAlerts(Times.Never());
    }

    [Fact]
    public async Task UnusedAlert_WithStudents_NoAlert()
    {
        var module = new Module { Name = "M1", Code = "M1", SystemPrompt = "p", CourseId = _course.Id };
        _db.Modules.Add(module);
        _db.SaveChanges();
        _db.ModuleAccessTokens.Add(new ModuleAccessToken { Token = "t", Name = "n", ModuleId = module.Id, CreatedAt = T0 });
        _db.SaveChanges();

        await RunAtAsync(T0.AddDays(10));

        VerifyUnusedAlerts(Times.Never());
    }

    [Fact]
    public async Task UnusedAlert_DeactivatedCourse_StopsAlerts()
    {
        MakeEmptyCourseWithKey(T0);
        await RunAtAsync(T0.AddDays(3));
        await _service.DeactivateAsync(_course.Id, _assignedProf);

        await RunAtAsync(T0.AddDays(4));

        VerifyUnusedAlerts(Times.Once());
    }

    // ── Course page actions ─────────────────────────────────────────────────

    [Fact]
    public async Task Actions_NonAssignedProfessor_Forbidden()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.PauseAsync(_course.Id, _otherProf));
        var info = await _service.GetAsync(_course.Id, _otherProf);
        Assert.False(info.CanManage);
    }

    [Fact]
    public async Task Actions_ManagerOfSameUniversity_Allowed()
    {
        var info = await _service.PauseAsync(_course.Id, _manager);
        Assert.True(info.Paused);
        Assert.True(info.CanManage);
    }

    [Fact]
    public async Task Actions_OtherUniversity_NotFound()
    {
        var outsider = AddUser("outsider", UserTypes.Manager, "x@other.br", universityId: _uni.Id + 100);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetAsync(_course.Id, outsider));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.PauseAsync(_course.Id, outsider));
    }

    [Fact]
    public async Task Actions_SuperAdmin_Allowed()
    {
        var admin = AddUser("root", UserTypes.SuperAdmin, "root@tutoria.br", universityId: null);
        var info = await _service.ExtendAsync(_course.Id, admin);
        Assert.Equal(7, info.ExtensionDays);
    }

    [Fact]
    public async Task Delete_RequiresInactiveCourse_ThenHidesIt()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(_course.Id, _assignedProf));

        var info = await _service.DeactivateAsync(_course.Id, _assignedProf);
        Assert.Equal("inactive", info.Status);

        await _service.DeleteAsync(_course.Id, _assignedProf);

        Assert.NotNull(Reload().DeletedAt);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetAsync(_course.Id, _assignedProf));
        var (items, _) = await new CourseRepository(_db).SearchAsync(_uni.Id, null, null, 1, 10);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Reactivate_RestoresCourse()
    {
        await _service.DeactivateAsync(_course.Id, _assignedProf);
        var info = await _service.ReactivateAsync(_course.Id, _assignedProf);

        Assert.Null(info.DeactivatedAt);
        Assert.NotEqual("inactive", info.Status);
    }

    [Fact]
    public async Task Get_ReportsTimeline()
    {
        _clock.Now = new DateTimeOffset(T0.AddDays(10), TimeSpan.Zero);
        var info = await _service.GetAsync(_course.Id, _assignedProf);

        Assert.Equal("running", info.Status);
        Assert.Equal(FirstEnrollment, info.StartedAt);
        Assert.Equal(Expires, info.ExpiresAt);
        Assert.Equal(Removal, info.RemovalAt);
        Assert.Equal(1, info.StudentsCount);
        Assert.True(info.CanManage);
    }

    [Fact]
    public async Task Get_NoStudentsYet_WaitingStatus()
    {
        _db.StudentCourses.RemoveRange(_db.StudentCourses);
        _db.SaveChanges();

        var info = await _service.GetAsync(_course.Id, _assignedProf);

        Assert.Equal("waiting_students", info.Status);
        Assert.Null(info.RemovalAt);
    }
}
