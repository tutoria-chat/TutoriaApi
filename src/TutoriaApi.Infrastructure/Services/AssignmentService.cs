using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IAssignmentRepository assignmentRepository,
        IModuleRepository moduleRepository,
        IBlobStorageService blobStorageService,
        ILogger<AssignmentService> logger)
    {
        _assignmentRepository = assignmentRepository;
        _moduleRepository = moduleRepository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<(List<Assignment> Items, int Total)> GetPagedAsync(
        int moduleId, int page, int pageSize, User currentUser)
    {
        await EnsureModuleAccessAsync(moduleId, currentUser);
        return await _assignmentRepository.GetPagedByModuleIdAsync(moduleId, page, pageSize);
    }

    public async Task<AssignmentWithDownloadUrl?> GetByIdAsync(int id, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id);
        if (assignment == null) return null;

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

        var downloadUrl = _blobStorageService.GetDownloadUrl(assignment.S3Key, expiresInHours: 1);
        return new AssignmentWithDownloadUrl { Assignment = assignment, DownloadUrl = downloadUrl };
    }

    public async Task<Assignment> CreateAsync(
        int moduleId, string title, string? description, DateTime dueDate,
        Stream fileStream, string originalFileName, string contentType, long fileSize,
        User currentUser)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        var university = module.Course?.University
            ?? throw new InvalidOperationException("Module is not linked to a university");

        if (!university.IsEnterprise || !university.HasAssignments)
            throw new InvalidOperationException(
                "Assignments feature requires an enterprise university with HasAssignments enabled");

        await EnsureModuleAccessAsync(moduleId, currentUser, module);

        var extension = Path.GetExtension(originalFileName);
        var s3Key = $"assignments/{moduleId}/{Guid.NewGuid()}{extension}";

        await _blobStorageService.UploadFileAsync(fileStream, s3Key, contentType);

        var assignment = new Assignment
        {
            ModuleId = moduleId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            S3Key = s3Key,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            CreatedByUserId = currentUser.UserId,
        };

        await _assignmentRepository.AddAsync(assignment);
        _logger.LogInformation("Created assignment '{Title}' for module {ModuleId}", title, moduleId);
        return assignment;
    }

    public async Task<Assignment> UpdateAsync(
        int id, string title, string? description, DateTime dueDate, User currentUser)
    {
        var assignment = await _assignmentRepository.GetByIdWithModuleAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found");

        await EnsureModuleAccessAsync(assignment.ModuleId, currentUser);

        assignment.Title = title;
        assignment.Description = description;
        assignment.DueDate = dueDate;

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
            return;
        }

        throw new UnauthorizedAccessException("Access denied");
    }
}
