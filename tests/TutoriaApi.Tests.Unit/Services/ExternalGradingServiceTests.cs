using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>External grading API paths on GradingJobService.</summary>
public class ExternalGradingServiceTests
{
    private readonly Mock<IGradingJobRepository> _jobRepo = new();
    private readonly Mock<ICourseRepository> _courseRepo = new();
    private readonly Mock<IUniversityRepository> _universityRepo = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<ISqsMessagingService> _sqs = new();
    private readonly GradingJobService _service;

    public ExternalGradingServiceTests()
    {
        _service = new GradingJobService(
            _jobRepo.Object, _courseRepo.Object, _universityRepo.Object, _blob.Object, _sqs.Object,
            new Mock<ILogger<GradingJobService>>().Object);
    }

    /// <summary>Sets up the job/blob/sqs mocks needed for a successful create.</summary>
    private void ArrangeJobPlumbing()
    {
        _jobRepo.Setup(r => r.AddAsync(It.IsAny<GradingJob>())).ReturnsAsync((GradingJob j) => { if (j.Id == 0) j.Id = 42; return j; });
        _jobRepo.Setup(r => r.UpdateAsync(It.IsAny<GradingJob>())).Returns(Task.CompletedTask);
        _blob.Setup(b => b.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("key");
        _sqs.Setup(s => s.SendGradingJobAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
    }

    private static Course CourseWithAssignments(bool enabled) => new()
    {
        Id = 5, Name = "C", Code = "C", UniversityId = 1, ExternalCourseId = 999,
        University = new University { Id = 1, Name = "U", Code = "U", HasAssignments = enabled },
    };

    private static MemoryStream Body() => new(Encoding.UTF8.GetBytes("[{\"name\":\"x\"}]"));

    [Fact]
    public async Task CreateExternalJobAsync_CourseNotFound_UniversityMissing_Throws()
    {
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync((Course?)null);
        _universityRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((University?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateExternalJobAsync(1, 999, Body(), null, null));
    }

    [Fact]
    public async Task CreateExternalJobAsync_CourseNotFound_AssignmentsDisabled_Throws()
    {
        // No course yet, and the university has grading off — must not auto-provision.
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync((Course?)null);
        _universityRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new University { Id = 1, Name = "U", Code = "U", HasAssignments = false });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateExternalJobAsync(1, 999, Body(), null, null));
        _courseRepo.Verify(r => r.AddAsync(It.IsAny<Course>()), Times.Never);
    }

    [Fact]
    public async Task CreateExternalJobAsync_CourseNotFound_AutoProvisionsAndGrades()
    {
        // First grading for a Moodle course the university never registered in
        // Tutoria: the course is created automatically so there is no manual setup.
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync((Course?)null);
        _universityRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new University { Id = 1, Name = "U", Code = "U", HasAssignments = true });

        Course? created = null;
        _courseRepo.Setup(r => r.AddAsync(It.IsAny<Course>()))
            .Callback<Course>(c => { c.Id = 77; created = c; })
            .ReturnsAsync((Course c) => c);
        ArrangeJobPlumbing();

        var job = await _service.CreateExternalJobAsync(1, 999, Body(), null, null, "Cálculo I");

        Assert.NotNull(created);
        Assert.Equal(999, created!.ExternalCourseId);
        Assert.Equal(1, created.UniversityId);
        Assert.Equal("Cálculo I", created.Name);          // name forwarded from the LMS
        Assert.Equal("MOODLE-999", created.Code);
        Assert.Equal(77, job.CourseId);                   // job keyed on the new course
        _sqs.Verify(s => s.SendGradingJobAsync(job.Id, 77), Times.Once);
    }

    [Fact]
    public async Task CreateExternalJobAsync_CourseNotFound_NoName_UsesFallbackName()
    {
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync((Course?)null);
        _universityRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new University { Id = 1, Name = "U", Code = "U", HasAssignments = true });
        Course? created = null;
        _courseRepo.Setup(r => r.AddAsync(It.IsAny<Course>()))
            .Callback<Course>(c => { c.Id = 77; created = c; }).ReturnsAsync((Course c) => c);
        ArrangeJobPlumbing();

        await _service.CreateExternalJobAsync(1, 999, Body(), null, null);

        Assert.Equal("Curso 999 (Moodle)", created!.Name);
    }

    [Fact]
    public async Task CreateExternalJobAsync_AssignmentsDisabled_Throws()
    {
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync(CourseWithAssignments(false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateExternalJobAsync(1, 999, Body(), null, null));
    }

    [Fact]
    public async Task CreateExternalJobAsync_Success_CreatesExternalJobAndEnqueues()
    {
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync(CourseWithAssignments(true));
        _jobRepo.Setup(r => r.AddAsync(It.IsAny<GradingJob>())).ReturnsAsync((GradingJob j) => { j.Id = 42; return j; });
        _jobRepo.Setup(r => r.UpdateAsync(It.IsAny<GradingJob>())).Returns(Task.CompletedTask);
        _blob.Setup(b => b.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("key");
        _sqs.Setup(s => s.SendGradingJobAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);

        var job = await _service.CreateExternalJobAsync(1, 999, Body(), null, null);

        Assert.Equal(42, job.Id);
        Assert.Equal(5, job.CourseId);
        Assert.Null(job.CreatedByUserId);
        Assert.Equal("external_api", job.Source);
        Assert.Equal("pending", job.Status);
        _sqs.Verify(s => s.SendGradingJobAsync(42, 5), Times.Once);
    }

    [Fact]
    public async Task GetExternalJobAsync_JobFromAnotherCourse_ReturnsNull()
    {
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync(CourseWithAssignments(true));
        _jobRepo.Setup(r => r.GetByIdWithCourseAsync(7))
            .ReturnsAsync(new GradingJob { Id = 7, CourseId = 88, Status = "completed" }); // different course

        var job = await _service.GetExternalJobAsync(1, 999, 7);
        Assert.Null(job);
    }

    [Fact]
    public async Task GetResultBytesAsync_NotCompleted_ReturnsNull()
    {
        var job = new GradingJob { Id = 1, CourseId = 5, Status = "processing", ResultS3Key = null };
        Assert.Null(await _service.GetResultBytesAsync(job));
    }
}
