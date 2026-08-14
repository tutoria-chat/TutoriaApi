using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IAssignmentRepository assignmentRepository,
        ICourseRepository courseRepository,
        IBlobStorageService blobStorageService,
        ILogger<AssignmentService> logger)
    {
        _assignmentRepository = assignmentRepository;
        _courseRepository = courseRepository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<(List<Assignment> Items, int Total)> GetPagedAsync(
        int courseId, int page, int pageSize, User currentUser)
    {
        var course = await EnsureCourseAccessAsync(courseId, currentUser);
        RequireAssignmentsFeature(course);
        return await _assignmentRepository.GetPagedByCourseIdAsync(courseId, page, pageSize);
    }

    public async Task<List<Assignment>> GetPublishedByCourseAsync(int courseId, User currentUser)
    {
        var course = await EnsureCourseAccessAsync(courseId, currentUser);
        RequireAssignmentsFeature(course);
        return await _assignmentRepository.GetPublishedByCourseIdAsync(courseId);
    }

    public async Task<AssignmentWithDownloadUrl?> GetByIdAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithCourseAsync(id);
        if (assignment == null) return null;

        await EnsureCourseAccessAsync(assignment.CourseId, currentUser, assignment.Course);

        var downloadUrl = _blobStorageService.GetDownloadUrl(assignment.S3Key, expiresInHours: 1);
        var rubricDownloadUrl = assignment.RubricS3Key != null
            ? _blobStorageService.GetDownloadUrl(assignment.RubricS3Key, expiresInHours: 1)
            : null;

        var contextFiles = assignment.ContextFiles
            .Select(f => (f, _blobStorageService.GetDownloadUrl(f.S3Key, expiresInHours: 1)))
            .ToList();

        return new AssignmentWithDownloadUrl
        {
            Assignment = assignment,
            DownloadUrl = downloadUrl,
            RubricDownloadUrl = rubricDownloadUrl,
            ContextFiles = contextFiles,
        };
    }

    public async Task<Assignment> CreateAsync(
        int courseId, string title, string? description, DateTime dueDate,
        string? keywords, string? gradingCriteria,
        Stream fileStream, string originalFileName, string contentType, long fileSize,
        Stream? rubricStream, string? rubricFileName, string? rubricContentType, long? rubricSize,
        User currentUser,
        List<ContextFileUpload>? contextFiles = null)
    {
        var course = await EnsureCourseAccessAsync(courseId, currentUser);

        if (course.University is not { HasAssignments: true })
            throw new InvalidOperationException(
                "Assignments feature is not enabled for this university");

        var extension = Path.GetExtension(originalFileName);
        var s3Key = $"assignments/courses/{courseId}/{Guid.NewGuid()}{extension}";
        await _blobStorageService.UploadFileAsync(fileStream, s3Key, contentType);

        string? rubricS3Key = null;
        if (rubricStream != null && rubricFileName != null && rubricContentType != null)
        {
            var rubricExtension = Path.GetExtension(rubricFileName);
            rubricS3Key = $"assignments/courses/{courseId}/rubric_{Guid.NewGuid()}{rubricExtension}";
            await _blobStorageService.UploadFileAsync(rubricStream, rubricS3Key, rubricContentType);
        }

        var assignment = new Assignment
        {
            CourseId = courseId,
            Title = title,
            Description = description,
            DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
            Keywords = keywords,
            GradingCriteria = string.IsNullOrWhiteSpace(gradingCriteria) ? null : gradingCriteria.Trim(),
            S3Key = s3Key,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            RubricS3Key = rubricS3Key,
            RubricOriginalFileName = rubricFileName,
            RubricContentType = rubricContentType,
            RubricFileSizeBytes = rubricSize,
            CreatedByUserId = currentUser.UserId,
        };

        await _assignmentRepository.AddAsync(assignment);

        if (contextFiles?.Count > 0)
        {
            var contextEntities = new List<AssignmentContextFile>();
            foreach (var cf in contextFiles)
            {
                var ext = Path.GetExtension(cf.FileName);
                var cfKey = $"assignments/courses/{courseId}/context_{Guid.NewGuid()}{ext}";
                await _blobStorageService.UploadFileAsync(cf.Stream, cfKey, cf.ContentType);
                contextEntities.Add(new AssignmentContextFile
                {
                    AssignmentId = assignment.Id,
                    S3Key = cfKey,
                    OriginalFileName = cf.FileName,
                    ContentType = cf.ContentType,
                    FileSizeBytes = cf.Size,
                });
            }
            await _assignmentRepository.AddContextFilesAsync(contextEntities);
        }

        _logger.LogInformation("Created assignment '{Title}' for course {CourseId} with {ContextCount} context files",
            title, courseId, contextFiles?.Count ?? 0);
        return assignment;
    }

    public async Task<Assignment> UpdateAsync(
        int id, string title, string? description, DateTime dueDate,
        string? keywords, string? gradingCriteria, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithCourseAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureCourseAccessAsync(assignment.CourseId, currentUser, assignment.Course);

        assignment.Title = title;
        assignment.Description = description;
        assignment.DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        assignment.Keywords = keywords;
        assignment.GradingCriteria = string.IsNullOrWhiteSpace(gradingCriteria) ? null : gradingCriteria.Trim();

        await _assignmentRepository.UpdateAsync(assignment);
        return assignment;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithCourseAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureCourseAccessAsync(assignment.CourseId, currentUser, assignment.Course);

        assignment.IsActive = false;
        await _assignmentRepository.UpdateAsync(assignment);
        _logger.LogInformation("Soft-deleted assignment {Id}", id);
    }

    public async Task<Assignment> TogglePublishAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithCourseAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureCourseAccessAsync(assignment.CourseId, currentUser, assignment.Course);

        assignment.IsPublished = !assignment.IsPublished;
        await _assignmentRepository.UpdateAsync(assignment);
        return assignment;
    }

    private static void RequireAssignmentsFeature(Course course)
    {
        if (course.University is not { HasAssignments: true })
            throw new UnauthorizedAccessException("Assignments feature is not enabled for this university");
    }

    private async Task<Course> EnsureCourseAccessAsync(int courseId, User currentUser, Course? cachedCourse = null)
    {
        var course = cachedCourse?.University != null
            ? cachedCourse
            : await _courseRepository.GetWithDetailsAsync(courseId)
                ?? throw new KeyNotFoundException($"Course {courseId} not found");

        if (currentUser.UserType == UserTypes.SuperAdmin) return course;

        // All university-scoped staff roles require only that the course belongs to their
        // university — the same rule the course detail page uses. Professors are not further
        // restricted to courses they are assigned to; if they can open the course page they
        // should also be able to see its assignments.
        if (currentUser.UserType is UserTypes.Manager or UserTypes.Tutor
            or UserTypes.PlatformCoordinator or UserTypes.Professor)
        {
            if (course.UniversityId != currentUser.UniversityId)
                throw new UnauthorizedAccessException("Access denied: course belongs to a different university");
            return course;
        }

        throw new UnauthorizedAccessException("Access denied");
    }
}
