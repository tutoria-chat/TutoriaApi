using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class CourseEventServiceTests
{
    private readonly Mock<ICourseEventRepository> _eventRepoMock = new();
    private readonly Mock<ICourseRepository> _courseRepoMock = new();
    private readonly Mock<IAssignmentRepository> _assignmentRepoMock = new();
    private readonly CourseEventService _service;

    private static readonly User SuperAdmin = new()
    {
        UserId = 1, Username = "root", Email = "root@t.com",
        FirstName = "Root", LastName = "Admin", UserType = "super_admin",
    };

    private static readonly User Manager = new()
    {
        UserId = 2, Username = "mgr", Email = "mgr@t.com",
        FirstName = "Uni", LastName = "Manager", UserType = "manager", UniversityId = 10,
    };

    public CourseEventServiceTests()
    {
        _service = new CourseEventService(
            _eventRepoMock.Object,
            _courseRepoMock.Object,
            _assignmentRepoMock.Object,
            Mock.Of<ILogger<CourseEventService>>());
    }

    private static Course CourseOfUniversity(int universityId = 10) =>
        new() { Id = 5, Name = "Course", Code = "C1", UniversityId = universityId };

    private static CourseEvent ValidEvent() => new()
    {
        CourseId = 5,
        Title = "Prova 1",
        EventType = "test",
        StartsAtUtc = DateTime.UtcNow.AddDays(7),
        Remind24Hours = true,
    };

    [Fact]
    public async Task CreateAsync_ValidEvent_SetsCreatorAndPersists()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<CourseEvent>()))
            .ReturnsAsync((CourseEvent e) => { e.Id = 99; return e; });

        var created = await _service.CreateAsync(ValidEvent(), Manager);

        Assert.Equal(99, created.Id);
        Assert.Equal(Manager.UserId, created.CreatedByUserId);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<CourseEvent>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvalidEventType_ThrowsInvalidOperationException()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        var ev = ValidEvent();
        ev.EventType = "party";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(ev, Manager));
    }

    [Fact]
    public async Task CreateAsync_EndsBeforeStarts_ThrowsInvalidOperationException()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        var ev = ValidEvent();
        ev.EndsAtUtc = ev.StartsAtUtc.AddHours(-2);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(ev, Manager));
    }

    [Fact]
    public async Task CreateAsync_DifferentUniversity_ThrowsUnauthorizedAccessException()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity(universityId: 77));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(ValidEvent(), Manager));
    }

    [Fact]
    public async Task CreateAsync_AssignmentAlreadyLinked_ThrowsInvalidOperationException()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        _eventRepoMock.Setup(r => r.GetByAssignmentIdAsync(42))
            .ReturnsAsync(new CourseEvent { CourseId = 5, Title = "x", EventType = "assignment" });

        var ev = ValidEvent();
        ev.AssignmentId = 42;
        ev.EventType = "assignment";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(ev, Manager));
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        _eventRepoMock.Setup(r => r.GetByIdWithCourseAsync(123)).ReturnsAsync((CourseEvent?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(123, ValidEvent(), SuperAdmin));
    }

    [Fact]
    public async Task GetCourseCalendarAsync_SynthesizesUnlinkedAssignments()
    {
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        _eventRepoMock.Setup(r => r.GetByCourseIdAsync(5, null, null)).ReturnsAsync(new List<CourseEvent>());
        _eventRepoMock.Setup(r => r.GetLinkedAssignmentIdsAsync(5)).ReturnsAsync(new List<int> { 1 });
        _assignmentRepoMock.Setup(r => r.GetPublishedByCourseIdAsync(5)).ReturnsAsync(new List<Assignment>
        {
            new()
            {
                Id = 1, ModuleId = 9, Title = "Linked", DueDate = DateTime.UtcNow.AddDays(1),
                S3Key = "k", OriginalFileName = "f.pdf", ContentType = "application/pdf",
            },
            new()
            {
                Id = 2, ModuleId = 9, Title = "Unlinked", DueDate = DateTime.UtcNow.AddDays(2),
                S3Key = "k2", OriginalFileName = "f2.pdf", ContentType = "application/pdf",
            },
        });

        var (events, unlinked) = await _service.GetCourseCalendarAsync(5, null, null, SuperAdmin);

        Assert.Empty(events);
        var assignment = Assert.Single(unlinked);
        Assert.Equal(2, assignment.Id);
    }

    [Fact]
    public async Task DeleteAsync_Existing_DeletesViaRepository()
    {
        var ev = ValidEvent();
        ev.Id = 7;
        _eventRepoMock.Setup(r => r.GetByIdWithCourseAsync(7)).ReturnsAsync(ev);
        _courseRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(CourseOfUniversity());
        _eventRepoMock.Setup(r => r.DeleteAsync(ev)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(7, Manager);

        _eventRepoMock.Verify(r => r.DeleteAsync(ev), Times.Once);
    }
}
