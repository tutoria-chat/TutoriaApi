using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class GradingJobService : IGradingJobService
{
    private readonly IGradingJobRepository _gradingJobRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly ILogger<GradingJobService> _logger;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public GradingJobService(
        IGradingJobRepository gradingJobRepository,
        ICourseRepository courseRepository,
        IUniversityRepository universityRepository,
        IBlobStorageService blobStorageService,
        ISqsMessagingService sqsMessagingService,
        ILogger<GradingJobService> logger)
    {
        _gradingJobRepository = gradingJobRepository;
        _courseRepository = courseRepository;
        _universityRepository = universityRepository;
        _blobStorageService = blobStorageService;
        _sqsMessagingService = sqsMessagingService;
        _logger = logger;
    }

    public async Task<GradingJob> CreateJobAsync(int courseId, Stream jsonStream, string fileName, User currentUser, string? gradingCriteria = null)
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
            ProcessedSubmissions = 0,
            OriginalFilename = Path.GetFileName(fileName),
            GradingCriteria = string.IsNullOrWhiteSpace(gradingCriteria) ? null : gradingCriteria.Trim()
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

    // ── External automation API ────────────────────────────────────────────────

    public async Task<GradingJob> CreateExternalJobAsync(
        int universityId, int externalCourseId, Stream jsonStream, string? fileName, string? gradingCriteria,
        string? courseName = null)
    {
        // Resolve the Tutoria course that mirrors this LMS course. Historically this
        // required an admin to hand-create a course and set its ExternalCourseId,
        // which was the main setup friction — so when it does not exist yet we
        // provision it automatically. The grading feature flag still gates it, so
        // only universities that have grading enabled ever get an auto-created
        // course, and the operation is idempotent per (university, externalCourseId).
        var course = await _courseRepository.GetByExternalCourseIdAsync(externalCourseId, universityId);

        if (course == null)
        {
            var owner = await _universityRepository.GetByIdAsync(universityId)
                ?? throw new KeyNotFoundException($"University {universityId} not found");
            if (!owner.HasAssignments)
                throw new UnauthorizedAccessException("Grading feature is not enabled for this university");

            course = await _courseRepository.AddAsync(new Course
            {
                Name = string.IsNullOrWhiteSpace(courseName)
                    ? $"Curso {externalCourseId} (Moodle)"
                    : courseName.Trim(),
                Code = $"MOODLE-{externalCourseId}",
                UniversityId = universityId,
                ExternalCourseId = externalCourseId,
            });
            course.University = owner;

            _logger.LogInformation(
                "Auto-provisioned Tutoria course {CourseId} for LMS course {ExternalId} (university {UniversityId})",
                course.Id, externalCourseId, universityId);
        }

        var university = course.University
            ?? await _universityRepository.GetByIdAsync(universityId)
            ?? throw new InvalidOperationException("Course is not linked to a university");
        if (!university.HasAssignments)
            throw new UnauthorizedAccessException("Grading feature is not enabled for this university");

        if (jsonStream.Length == 0)
            throw new InvalidOperationException("Request body (submissions JSON) is empty");
        if (jsonStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("Submissions JSON exceeds the 10 MB limit");

        var job = new GradingJob
        {
            CourseId = course.Id,
            CreatedByUserId = null,          // external API — no user
            Source = "external_api",
            Status = "pending",
            TotalSubmissions = 0,
            ProcessedSubmissions = 0,
            OriginalFilename = Path.GetFileName(fileName ?? "external-submissions.json"),
            GradingCriteria = string.IsNullOrWhiteSpace(gradingCriteria) ? null : gradingCriteria.Trim(),
        };

        var created = await _gradingJobRepository.AddAsync(job);

        var s3Key = $"grading-jobs/{course.Id}/{created.Id}/input.json";
        try
        {
            await _blobStorageService.UploadFileAsync(jsonStream, s3Key, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload external grading input to S3 for job {JobId}", created.Id);
            created.Status = "failed";
            created.ErrorMessage = "Failed to store submissions";
            await _gradingJobRepository.UpdateAsync(created);
            throw;
        }

        created.InputS3Key = s3Key;
        await _gradingJobRepository.UpdateAsync(created);

        var sent = await _sqsMessagingService.SendGradingJobAsync(created.Id, course.Id);
        if (!sent)
            _logger.LogWarning("SQS message not sent for external grading job {JobId}", created.Id);

        _logger.LogInformation(
            "Created external grading job {JobId} for course {CourseId} (external {ExternalId}, university {UniversityId})",
            created.Id, course.Id, externalCourseId, universityId);

        return created;
    }

    public async Task<GradingJob?> GetExternalJobAsync(int universityId, int externalCourseId, int jobId)
    {
        var course = await _courseRepository.GetByExternalCourseIdAsync(externalCourseId, universityId);
        if (course == null) return null;

        var job = await _gradingJobRepository.GetByIdWithCourseAsync(jobId);
        // The job must belong to exactly this course — prevents cross-course/tenant access.
        if (job == null || job.CourseId != course.Id) return null;

        return job;
    }

    public async Task<byte[]?> GetResultBytesAsync(GradingJob job)
    {
        if (job.Status != "completed" || string.IsNullOrEmpty(job.ResultS3Key))
            return null;
        return await _blobStorageService.GetFileContentAsync(job.ResultS3Key);
    }
}
