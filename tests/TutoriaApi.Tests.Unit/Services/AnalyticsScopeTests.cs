using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using TutoriaApi.Infrastructure.Services;
using TutoriaApi.Web.API.Controllers;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Professors may use analytics, but ONLY for the courses they're assigned to.
/// Real repositories on the EF in-memory provider.
/// </summary>
public class AnalyticsScopeTests : IDisposable
{
    private readonly TutoriaDbContext _db;
    private readonly AnalyticsScopeService _scopes;
    private readonly University _uniA, _uniB;
    private readonly Course _calc, _physics, _otherUniCourse, _deleted;
    private readonly User _prof, _adminProf, _manager, _superAdmin;

    public AnalyticsScopeTests()
    {
        _db = new TutoriaDbContext(new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _uniA = new University { Name = "A", Code = "A" };
        _uniB = new University { Name = "B", Code = "B" };
        _db.Universities.AddRange(_uniA, _uniB);
        _db.SaveChanges();

        _calc = new Course { Name = "Cálculo", Code = "C1", UniversityId = _uniA.Id };
        _physics = new Course { Name = "Física", Code = "F1", UniversityId = _uniA.Id };      // another professor's
        _otherUniCourse = new Course { Name = "Outra", Code = "O1", UniversityId = _uniB.Id };
        _deleted = new Course { Name = "Velha", Code = "V1", UniversityId = _uniA.Id, DeletedAt = DateTime.UtcNow };
        _db.Courses.AddRange(_calc, _physics, _otherUniCourse, _deleted);
        _db.SaveChanges();

        _prof = AddUser("prof", UserTypes.Professor, _uniA.Id);
        _adminProf = AddUser("adminprof", UserTypes.Professor, _uniA.Id, isAdmin: true);
        _manager = AddUser("manager", UserTypes.Manager, _uniA.Id);
        _superAdmin = AddUser("root", UserTypes.SuperAdmin, null);

        // prof teaches Cálculo; a stale assignment to another university's course and a deleted course must not count
        _db.ProfessorCourses.AddRange(
            new ProfessorCourse { ProfessorId = _prof.UserId, CourseId = _calc.Id },
            new ProfessorCourse { ProfessorId = _prof.UserId, CourseId = _otherUniCourse.Id },
            new ProfessorCourse { ProfessorId = _prof.UserId, CourseId = _deleted.Id });
        _db.SaveChanges();

        _scopes = new AnalyticsScopeService(new CourseRepository(_db), new ProfessorCourseRepository(_db), new UserRepository(_db));
    }

    public void Dispose() => _db.Dispose();

    private User AddUser(string username, string type, int? universityId, bool isAdmin = false)
    {
        var user = new User
        {
            Username = username, Email = $"{username}@x.br", FirstName = username, LastName = "X",
            UserType = type, UniversityId = universityId, IsAdmin = isAdmin,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private Task<AnalyticsScope> Resolve(User u, int? requestedUniversity = null)
        => _scopes.ResolveAsync(u.UserId, u.UserType, u.UniversityId, requestedUniversity);

    // ── Scope resolution ────────────────────────────────────────────────────

    [Fact]
    public async Task Professor_SeesOnlyAssignedCoursesOfOwnUniversity()
    {
        var scope = await Resolve(_prof);

        Assert.True(scope.IsCourseRestricted);
        Assert.Equal(new[] { _calc.Id }, scope.CourseIds);
        Assert.False(scope.CanSeeCourse(_physics.Id));
        Assert.False(scope.CanSeeCourse(_otherUniCourse.Id));
        Assert.False(scope.CanSeeCourse(_deleted.Id));
    }

    [Fact]
    public async Task Professor_CannotWidenScopeWithUniversityParameter()
    {
        var scope = await Resolve(_prof, requestedUniversity: _uniB.Id);
        Assert.Equal(new[] { _calc.Id }, scope.CourseIds);
    }

    [Fact]
    public async Task Professor_WithNoAssignments_SeesNothing()
    {
        var lonely = AddUser("lonely", UserTypes.Professor, _uniA.Id);
        var scope = await Resolve(lonely);
        Assert.Empty(scope.CourseIds);
    }

    [Fact]
    public async Task AdminProfessor_SeesWholeUniversity()
    {
        var scope = await Resolve(_adminProf);
        Assert.False(scope.IsCourseRestricted);
        Assert.Equal(new[] { _calc.Id, _physics.Id }.OrderBy(i => i), scope.CourseIds.OrderBy(i => i));
    }

    [Fact]
    public async Task Manager_SeesWholeOwnUniversity_NotOthers()
    {
        var scope = await Resolve(_manager, requestedUniversity: _uniB.Id);
        Assert.Equal(new[] { _calc.Id, _physics.Id }.OrderBy(i => i), scope.CourseIds.OrderBy(i => i));
    }

    [Fact]
    public async Task SuperAdmin_SeesAll_OrOneUniversity()
    {
        var all = await Resolve(_superAdmin);
        Assert.True(all.IsSuperAdmin);
        Assert.Equal(3, all.CourseIds.Count); // deleted excluded

        var one = await Resolve(_superAdmin, requestedUniversity: _uniB.Id);
        Assert.Equal(new[] { _otherUniCourse.Id }, one.CourseIds);
    }

    // ── Through the services ────────────────────────────────────────────────

    private AnalyticsService Analytics()
        => new(
            Mock.Of<IDynamoDbAnalyticsService>(), new ModuleRepository(_db), new CourseRepository(_db),
            Mock.Of<IUniversityRepository>(), Mock.Of<IAIModelRepository>(), Mock.Of<IFileRepository>(),
            Mock.Of<IAnalyticsDailySummaryRepository>(), Mock.Of<ITopicClassificationRepository>(),
            Mock.Of<IQuizAnalyticsRepository>(), new StudentCourseRepository(_db), new UserRepository(_db),
            Mock.Of<IDailyAISummaryRepository>(), new GamificationStatsRepository(_db), _scopes,
            Mock.Of<ILogger<AnalyticsService>>());

    [Fact]
    public async Task ModuleAccess_ProfessorOnlyOwnCoursesModules()
    {
        var own = new TutoriaApi.Core.Entities.Module { Name = "M1", Code = "M1", SystemPrompt = "p", CourseId = _calc.Id };
        var others = new TutoriaApi.Core.Entities.Module { Name = "M2", Code = "M2", SystemPrompt = "p", CourseId = _physics.Id };
        _db.Modules.AddRange(own, others);
        _db.SaveChanges();

        var service = Analytics();
        Assert.True(await service.CanAccessModuleAsync(_prof.UserId, _prof.UserType, _prof.UniversityId, own.Id));
        Assert.False(await service.CanAccessModuleAsync(_prof.UserId, _prof.UserType, _prof.UniversityId, others.Id));
        Assert.True(await service.CanAccessModuleAsync(_manager.UserId, _manager.UserType, _manager.UniversityId, others.Id));
    }

    [Fact]
    public async Task Rankings_ProfessorSeesOnlyOwnStudents_AndOnlyActivityInOwnCourses()
    {
        var mine = AddUser("ana", UserTypes.Student, _uniA.Id);
        var theirs = AddUser("bruno", UserTypes.Student, _uniA.Id);
        _db.StudentCourses.AddRange(
            new StudentCourse { StudentId = mine.UserId, CourseId = _calc.Id },
            new StudentCourse { StudentId = theirs.UserId, CourseId = _physics.Id });
        var now = DateTime.UtcNow;
        _db.StudentActivities.AddRange(
            new StudentActivity { StudentId = mine.UserId, CourseId = _calc.Id, ActivityType = "chat_question", Xp = 10, OccurredAt = now },
            // Ana's activity in someone else's course must not count for this professor
            new StudentActivity { StudentId = mine.UserId, CourseId = _physics.Id, ActivityType = "chat_question", Xp = 10, OccurredAt = now },
            new StudentActivity { StudentId = mine.UserId, CourseId = _physics.Id, ActivityType = "quiz_completed", Xp = 10, OccurredAt = now },
            new StudentActivity { StudentId = theirs.UserId, CourseId = _physics.Id, ActivityType = "chat_question", Xp = 10, OccurredAt = now });
        _db.SaveChanges();

        var rankings = await Analytics().GetRankingsAsync(
            _prof.UserId, _prof.UserType, _prof.UniversityId, null, now.AddDays(-1), now.AddDays(1));

        Assert.Equal(1, rankings.TotalStudents);
        var active = Assert.Single(rankings.MostActive);
        Assert.Equal(mine.UserId, active.StudentId);
        Assert.Equal(1, active.Value);
        Assert.Equal(1, rankings.Engagement.Questions);
        Assert.Equal(0, rankings.Engagement.Quizzes);
        Assert.DoesNotContain(rankings.MostImproved, r => r.StudentId == theirs.UserId);
    }

    [Fact]
    public async Task CourseModuleStats_OtherProfessorsCourse_ReturnsNothing()
    {
        var stats = new InstitutionStatsService(
            new CourseRepository(_db), new ModuleRepository(_db), new StudentCourseRepository(_db),
            Mock.Of<IQuizAnalyticsRepository>(), new GamificationStatsRepository(_db), Mock.Of<IUniversityRepository>(),
            _scopes, Mock.Of<ILogger<InstitutionStatsService>>());

        var others = await stats.GetCourseModuleStatsAsync(_prof.UserId, _prof.UserType, _prof.UniversityId, _physics.Id);
        Assert.True(string.IsNullOrEmpty(others.CourseName));
        Assert.Empty(others.Modules);

        var own = await stats.GetCourseModuleStatsAsync(_prof.UserId, _prof.UserType, _prof.UniversityId, _calc.Id);
        Assert.Equal("Cálculo", own.CourseName);

        var courses = await stats.GetCourseStatsAsync(_prof.UserId, _prof.UserType, _prof.UniversityId, null);
        Assert.Equal(new[] { _calc.Id }, courses.Courses.Select(c => c.CourseId));
    }

    // ── Which endpoints a professor can reach at all ────────────────────────

    [Fact]
    public void Controller_OnlyTheScopedEndpointsAreOpenToProfessors()
    {
        var expectedOpen = new[]
        {
            "dashboard/summary", "at-risk-students", "risk-predictions", "modules/questions-per-module",
            "topics/most-demanded", "quiz/performance", "rankings",
        };

        Assert.Equal("AnalyticsViewer", typeof(AnalyticsController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);

        var open = typeof(AnalyticsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (Route: m.GetCustomAttributes().OfType<HttpMethodAttribute>().FirstOrDefault()?.Template,
                          Policies: m.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList()))
            .Where(x => x.Route != null && !x.Policies.Contains("AnalyticsAccess") && !x.Policies.Contains("SuperAdminOnly"))
            .Select(x => x.Route!)
            .OrderBy(r => r)
            .ToList();

        Assert.Equal(expectedOpen.OrderBy(r => r), open);
    }
}
