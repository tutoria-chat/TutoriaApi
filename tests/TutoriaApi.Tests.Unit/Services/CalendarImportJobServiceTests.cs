using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class CalendarImportJobServiceTests
{
    private readonly Mock<ICalendarImportJobRepository> _jobRepo = new();
    private readonly Mock<ICourseRepository> _courseRepo = new();
    private readonly Mock<ICourseEventService> _eventService = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IConfiguration> _config = new();
    private readonly Mock<IHttpClientFactory> _httpFactory = new();
    private readonly CalendarImportJobService _service;

    public CalendarImportJobServiceTests()
    {
        _service = new CalendarImportJobService(
            _jobRepo.Object, _courseRepo.Object, _eventService.Object, _blob.Object,
            _config.Object, _httpFactory.Object, new Mock<ILogger<CalendarImportJobService>>().Object);
    }

    private static User SuperAdmin() => new()
    {
        UserId = 1, UserType = UserTypes.SuperAdmin,
        Username = "admin", Email = "admin@test.com", FirstName = "Super", LastName = "Admin",
    };

    [Fact]
    public async Task ConfirmAsync_CreatesEventsAndMarksConfirmed()
    {
        var job = new CalendarImportJob { Id = 5, CourseId = 10, Status = "completed" };
        _jobRepo.Setup(r => r.GetWithCourseAsync(5)).ReturnsAsync(job);
        _eventService.Setup(s => s.CreateAsync(It.IsAny<CourseEvent>(), It.IsAny<User>()))
            .ReturnsAsync((CourseEvent e, User _) => e);

        var events = new List<CourseEvent>
        {
            new() { Title = "Prova 1", EventType = "test", StartsAtUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) },
            new() { Title = "Entrega TCC", EventType = "assignment", StartsAtUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc) },
        };

        var created = await _service.ConfirmAsync(5, events, SuperAdmin());

        Assert.Equal(2, created);
        Assert.Equal("confirmed", job.Status);
        Assert.Equal(2, job.ConfirmedCount);
        Assert.NotNull(job.ConfirmedAt);
        // every confirmed event is forced onto the job's course
        _eventService.Verify(s => s.CreateAsync(It.Is<CourseEvent>(e => e.CourseId == 10), It.IsAny<User>()), Times.Exactly(2));
        _jobRepo.Verify(r => r.UpdateAsync(job), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_JobNotFound_Throws()
    {
        _jobRepo.Setup(r => r.GetWithCourseAsync(999)).ReturnsAsync((CalendarImportJob?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.ConfirmAsync(999, new List<CourseEvent>(), SuperAdmin()));
    }

    [Fact]
    public async Task GetJobsForCourseAsync_ManagerDifferentUniversity_Throws()
    {
        var manager = new User
        {
            UserId = 2, UserType = UserTypes.Manager, UniversityId = 1,
            Username = "mgr", Email = "mgr@test.com", FirstName = "Man", LastName = "Ager",
        };
        _courseRepo.Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(new Course { Id = 10, UniversityId = 99, Name = "Course", Code = "C10" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetJobsForCourseAsync(10, manager));
    }
}
