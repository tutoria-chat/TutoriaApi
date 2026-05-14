using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IProfessorCourseRepository _professorCourseRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IAssignmentRepository assignmentRepository,
        IModuleRepository moduleRepository,
        IProfessorCourseRepository professorCourseRepository,
        IBlobStorageService blobStorageService,
        ILogger<AssignmentService> logger)
    {
        _assignmentRepository = assignmentRepository;
        _moduleRepository = moduleRepository;
        _professorCourseRepository = professorCourseRepository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<(List<Assignment> Items, int Total)> GetPagedAsync(
        int moduleId, int page, int pageSize, User currentUser)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        var university = module.Course?.University
            ?? throw new InvalidOperationException("Module is not linked to a university");

        if (!university.HasAssignments)
            throw new UnauthorizedAccessException("Assignments feature is not enabled for this university");

        await EnsureModuleAccessAsync(moduleId, currentUser, module);
        return await _assignmentRepository.GetPagedByModuleIdAsync(moduleId, page, pageSize);
    }

    public async Task<AssignmentWithDownloadUrl?> GetByIdAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id);
        if (assignment == null) return null;

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

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
        int moduleId, string title, string? description, DateTime dueDate,
        string? keywords,
        Stream fileStream, string originalFileName, string contentType, long fileSize,
        Stream? rubricStream, string? rubricFileName, string? rubricContentType, long? rubricSize,
        User currentUser,
        List<ContextFileUpload>? contextFiles = null)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        var university = module.Course?.University
            ?? throw new InvalidOperationException("Module is not linked to a university");

        if (!university.HasAssignments)
            throw new InvalidOperationException(
                "Assignments feature is not enabled for this university");

        await EnsureModuleAccessAsync(moduleId, currentUser, module);

        var extension = Path.GetExtension(originalFileName);
        var s3Key = $"assignments/{moduleId}/{Guid.NewGuid()}{extension}";
        await _blobStorageService.UploadFileAsync(fileStream, s3Key, contentType);

        string? rubricS3Key = null;
        if (rubricStream != null && rubricFileName != null && rubricContentType != null)
        {
            var rubricExtension = Path.GetExtension(rubricFileName);
            rubricS3Key = $"assignments/{moduleId}/rubric_{Guid.NewGuid()}{rubricExtension}";
            await _blobStorageService.UploadFileAsync(rubricStream, rubricS3Key, rubricContentType);
        }

        var assignment = new Assignment
        {
            ModuleId = moduleId,
            Title = title,
            Description = description,
            DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
            Keywords = keywords,
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
                var cfKey = $"assignments/{moduleId}/context_{Guid.NewGuid()}{ext}";
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

        _logger.LogInformation("Created assignment '{Title}' for module {ModuleId} with {ContextCount} context files",
            title, moduleId, contextFiles?.Count ?? 0);
        return assignment;
    }

    public async Task<Assignment> UpdateAsync(
        int id, string title, string? description, DateTime dueDate,
        string? keywords, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

        assignment.Title = title;
        assignment.Description = description;
        assignment.DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        assignment.Keywords = keywords;

        await _assignmentRepository.UpdateAsync(assignment);
        return assignment;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

        assignment.IsActive = false;
        await _assignmentRepository.UpdateAsync(assignment);
        _logger.LogInformation("Soft-deleted assignment {Id}", id);
    }

    public async Task<Assignment> TogglePublishAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

        assignment.IsPublished = !assignment.IsPublished;
        await _assignmentRepository.UpdateAsync(assignment);
        return assignment;
    }

    private async Task EnsureModuleAccessAsync(int moduleId, User currentUser, Module? cachedModule = null)
    {
        if (currentUser.UserType == UserTypes.SuperAdmin) return;

        var module = cachedModule ?? await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        var moduleUniversityId = module.Course?.UniversityId;

        if (currentUser.UserType is UserTypes.Manager or UserTypes.Tutor or UserTypes.PlatformCoordinator)
        {
            if (moduleUniversityId != currentUser.UniversityId)
                throw new UnauthorizedAccessException("Access denied: module belongs to a different university");
            return;
        }

        if (currentUser.UserType == UserTypes.Professor)
        {
            if (moduleUniversityId != currentUser.UniversityId)
                throw new UnauthorizedAccessException("Access denied: module belongs to a different university");

            // IsAdmin professors keep university-wide access for backwards compat;
            // regular professors must be assigned to the module's course.
            if (!(currentUser.IsAdmin ?? false))
            {
                var assigned = await _professorCourseRepository.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId, module.CourseId);
                if (!assigned)
                    throw new UnauthorizedAccessException("Access denied: not assigned to this course");
            }
            return;
        }

        throw new UnauthorizedAccessException("Access denied");
    }
}
