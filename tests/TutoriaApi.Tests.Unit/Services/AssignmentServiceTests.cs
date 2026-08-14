using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Assignments are course-scoped: access is decided by the assignment's course, never a module.
/// </summary>
public class AssignmentServiceTests
{
    private readonly Mock<IAssignmentRepository> _assignmentRepoMock = new();
    private readonly Mock<ICourseRepository> _courseRepoMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly AssignmentService _service;

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

    private static readonly User Student = new()
    {
        UserId = 3, Username = "stu", Email = "stu@t.com",
        FirstName = "A", LastName = "Student", UserType = "student", UniversityId = 10,
    };

    public AssignmentServiceTests()
    {
        _service = new AssignmentService(
            _assignmentRepoMock.Object,
            _courseRepoMock.Object,
            _blobMock.Object,
            Mock.Of<ILogger<AssignmentService>>());
    }

    private static Course CourseWith(bool hasAssignments = true, int universityId = 10) => new()
    {
        Id = 5,
        Name = "Course",
        Code = "C1",
        UniversityId = universityId,
        University = new University
        {
            Id = universityId, Name = "Uni", Code = "U", HasAssignments = hasAssignments,
        },
    };

    private static Assignment AssignmentOfCourse(Course course) => new()
    {
        Id = 7,
        CourseId = course.Id,
        Course = course,
        Title = "Ensaio",
        DueDate = DateTime.UtcNow.AddDays(3),
        S3Key = "k",
        OriginalFileName = "f.pdf",
        ContentType = "application/pdf",
    };

    [Fact]
    public async Task GetPagedAsync_CourseInSameUniversity_ReturnsAssignments()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith());
        _assignmentRepoMock.Setup(r => r.GetPagedByCourseIdAsync(5, 1, 20, true))
            .ReturnsAsync((new List<Assignment>(), 0));

        var (items, total) = await _service.GetPagedAsync(5, 1, 20, Manager);

        Assert.Empty(items);
        Assert.Equal(0, total);
        _assignmentRepoMock.Verify(r => r.GetPagedByCourseIdAsync(5, 1, 20, true), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_CourseInOtherUniversity_Throws()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith(universityId: 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetPagedAsync(5, 1, 20, Manager));
    }

    [Fact]
    public async Task GetPagedAsync_UnknownCourse_ThrowsKeyNotFound()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync((Course?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPagedAsync(5, 1, 20, Manager));
    }

    [Fact]
    public async Task GetPagedAsync_UniversityWithoutAssignmentsFeature_Throws()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith(hasAssignments: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetPagedAsync(5, 1, 20, Manager));
    }

    [Fact]
    public async Task GetPagedAsync_StudentRole_Throws()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetPagedAsync(5, 1, 20, Student));
    }

    [Fact]
    public async Task GetPublishedByCourseAsync_SuperAdmin_SkipsUniversityCheck()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith(universityId: 99));
        _assignmentRepoMock.Setup(r => r.GetPublishedByCourseIdAsync(5)).ReturnsAsync([]);

        var result = await _service.GetPublishedByCourseAsync(5, SuperAdmin);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7)).ReturnsAsync((Assignment?)null);

        Assert.Null(await _service.GetByIdAsync(7, Manager));
    }

    [Fact]
    public async Task GetByIdAsync_UsesCourseLoadedWithAssignment_WithoutExtraCourseLookup()
    {
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7))
            .ReturnsAsync(AssignmentOfCourse(CourseWith()));
        _blobMock.Setup(b => b.GetDownloadUrl("k", 1)).Returns("https://blob/k");

        var result = await _service.GetByIdAsync(7, Manager);

        Assert.NotNull(result);
        Assert.Equal("https://blob/k", result!.DownloadUrl);
        _courseRepoMock.Verify(r => r.GetWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_AssignmentFromOtherUniversity_Throws()
    {
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7))
            .ReturnsAsync(AssignmentOfCourse(CourseWith(universityId: 99)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetByIdAsync(7, Manager));
    }

    [Fact]
    public async Task CreateAsync_StoresCourseIdAndCourseScopedS3Key()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith());
        Assignment? saved = null;
        _assignmentRepoMock.Setup(r => r.AddAsync(It.IsAny<Assignment>()))
            .ReturnsAsync((Assignment a) => { a.Id = 42; saved = a; return a; });

        using var stream = new MemoryStream([1, 2, 3]);
        var created = await _service.CreateAsync(
            5, "Ensaio", "desc", DateTime.UtcNow.AddDays(3), "arg,estrutura", " criterios ",
            stream, "tarefa.pdf", "application/pdf", 3,
            null, null, null, null, Manager);

        Assert.Equal(5, created.CourseId);
        Assert.Equal("criterios", created.GradingCriteria);
        Assert.NotNull(saved);
        Assert.StartsWith("assignments/courses/5/", saved!.S3Key);
    }

    [Fact]
    public async Task CreateAsync_UniversityWithoutAssignmentsFeature_Throws()
    {
        _courseRepoMock.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(CourseWith(hasAssignments: false));

        using var stream = new MemoryStream([1]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(
                5, "Ensaio", null, DateTime.UtcNow.AddDays(3), null, null,
                stream, "t.pdf", "application/pdf", 1,
                null, null, null, null, Manager));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesInsteadOfRemoving()
    {
        var assignment = AssignmentOfCourse(CourseWith());
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7)).ReturnsAsync(assignment);

        await _service.DeleteAsync(7, Manager);

        Assert.False(assignment.IsActive);
        _assignmentRepoMock.Verify(r => r.UpdateAsync(assignment), Times.Once);
    }

    [Fact]
    public async Task TogglePublishAsync_FlipsFlag()
    {
        var assignment = AssignmentOfCourse(CourseWith());
        assignment.IsPublished = false;
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7)).ReturnsAsync(assignment);

        var result = await _service.TogglePublishAsync(7, Manager);

        Assert.True(result.IsPublished);
    }

    [Fact]
    public async Task UpdateAsync_UnknownAssignment_ThrowsKeyNotFound()
    {
        _assignmentRepoMock.Setup(r => r.GetByIdWithCourseAsync(7)).ReturnsAsync((Assignment?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateAsync(7, "t", null, DateTime.UtcNow, null, null, Manager));
    }
}
