using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class GradingJobService : IGradingJobService
{
    private readonly IGradingJobRepository _gradingJobRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly ILogger<GradingJobService> _logger;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public GradingJobService(
        IGradingJobRepository gradingJobRepository,
        ICourseRepository courseRepository,
        IBlobStorageService blobStorageService,
        ISqsMessagingService sqsMessagingService,
        ILogger<GradingJobService> logger)
    {
        _gradingJobRepository = gradingJobRepository;
        _courseRepository = courseRepository;
        _blobStorageService = blobStorageService;
        _sqsMessagingService = sqsMessagingService;
        _logger = logger;
    }

    public async Task<GradingJob> CreateJobAsync(int courseId, Stream jsonStream, string fileName, User currentUser)
    {
        // Load course with university info
        var course = await _courseRepository.GetWithDetailsAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found");

        var university = course.University
            ?? throw new InvalidOperationException("Course is not linked to a university");

        if (!university.HasAssignments)
            throw new UnauthorizedAccessException("Grading feature is not enabled for this university");

        // University-scope check: non-super-admins must belong to this university
        if (currentUser.UserType != UserTypes.SuperAdmin
            && currentUser.UniversityId.HasValue
            && currentUser.UniversityId.Value != course.UniversityId)
        {
            throw new UnauthorizedAccessException("You do not have access to this course");
        }

        if (jsonStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds the 10 MB limit");

        // Create job record first so we have the ID for the S3 path
        var job = new GradingJob
        {
            CourseId = courseId,
            CreatedByUserId = currentUser.UserId,
            Status = "pending",
            TotalSubmissions = 0,
            ProcessedSubmissions = 0
        };

        var created = await _gradingJobRepository.AddAsync(job);

        // Upload JSON to S3: grading-jobs/{courseId}/{jobId}/input.json
        var s3Key = $"grading-jobs/{courseId}/{created.Id}/input.json";
        try
        {
            await _blobStorageService.UploadFileAsync(jsonStream, s3Key, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload grading job input to S3 for job {JobId}", created.Id);
            created.Status = "failed";
            created.ErrorMessage = "Failed to upload input file";
            await _gradingJobRepository.UpdateAsync(created);
            throw;
        }

        created.InputS3Key = s3Key;
        await _gradingJobRepository.UpdateAsync(created);

        // Enqueue SQS message (non-blocking: worker processes asynchronously)
        var sent = await _sqsMessagingService.SendGradingJobAsync(created.Id, courseId);
        if (!sent)
        {
            _logger.LogWarning(
                "SQS message not sent for grading job {JobId} — job created but may not process automatically",
                created.Id);
        }

        _logger.LogInformation(
            "Created grading job {JobId} for course {CourseId} by user {UserId}",
            created.Id, courseId, currentUser.UserId);

        return created;
    }

    public async Task<List<GradingJob>> GetJobsForCourseAsync(int courseId, User currentUser)
    {
        var course = await _courseRepository.GetWithDetailsAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found");

        var university = course.University
            ?? throw new InvalidOperationException("Course is not linked to a university");

        if (!university.HasAssignments)
            throw new UnauthorizedAccessException("Grading feature is not enabled for this university");

        if (currentUser.UserType != UserTypes.SuperAdmin
            && currentUser.UniversityId.HasValue
            && currentUser.UniversityId.Value != course.UniversityId)
        {
            throw new UnauthorizedAccessException("You do not have access to this course");
        }

        return await _gradingJobRepository.GetByCourseIdAsync(courseId);
    }

    public async Task<string> GetDownloadUrlAsync(int jobId, User currentUser)
    {
        var job = await _gradingJobRepository.GetByIdWithCourseAsync(jobId)
            ?? throw new KeyNotFoundException($"Grading job {jobId} not found");

        var university = job.Course?.University
            ?? throw new InvalidOperationException("Job is not linked to a university");

        if (!university.HasAssignments)
            throw new UnauthorizedAccessException("Grading feature is not enabled for this university");

        if (currentUser.UserType != UserTypes.SuperAdmin
            && currentUser.UniversityId.HasValue
            && currentUser.UniversityId.Value != job.Course!.UniversityId)
        {
            throw new UnauthorizedAccessException("You do not have access to this grading job");
        }

        if (job.Status != "completed" || string.IsNullOrEmpty(job.ResultS3Key))
            throw new InvalidOperationException("Result is not available yet");

        // 24-hour pre-signed URL so professors can return later to download
        return _blobStorageService.GetDownloadUrl(job.ResultS3Key, expiresInHours: 24);
    }
}
