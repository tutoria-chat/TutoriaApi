using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly IFileRepository _fileRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly AccessControlHelper _accessControl;
    private readonly IAuditLogService _auditLogService;

    public FileService(
        IFileRepository fileRepository,
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        IBlobStorageService blobStorageService,
        AccessControlHelper accessControl,
        IAuditLogService auditLogService)
    {
        _fileRepository = fileRepository;
        _moduleRepository = moduleRepository;
        _courseRepository = courseRepository;
        _blobStorageService = blobStorageService;
        _accessControl = accessControl;
        _auditLogService = auditLogService;
    }

    public async Task<List<int>> GetAccessibleModuleIdsAsync(User user)
    {
        if (user.UserType == UserTypes.SuperAdmin)
        {
            // Super admins can access all modules
            var allModules = await _moduleRepository.GetAllAsync();
            return allModules.Select(m => m.Id).ToList();
        }

        // Manager, Tutor, Platform Coordinator have university-scoped access
        if (user.UserType == UserTypes.Manager ||
            user.UserType == UserTypes.Tutor ||
            user.UserType == UserTypes.PlatformCoordinator)
        {
            // University-scoped roles can access all modules in their university
            var universityModules = await _moduleRepository.GetByUniversityIdAsync(user.UniversityId ?? 0);
            return universityModules.Select(m => m.Id).ToList();
        }

        // Professors (both admin and regular) have university-scoped access,
        // consistent with their read access via CallerOwnsModuleAsync in ModulesController.
        if (user.UserType == UserTypes.Professor)
        {
            var universityModules = await _moduleRepository.GetByUniversityIdAsync(user.UniversityId ?? 0);
            return universityModules.Select(m => m.Id).ToList();
        }

        return new List<int>();
    }

    public async Task<(List<FileEntity> Items, int Total)> GetPagedFilesAsync(
        int? moduleId,
        string? search,
        int page,
        int pageSize,
        User currentUser)
    {
        var accessibleModuleIds = await GetAccessibleModuleIdsAsync(currentUser);

        var (items, total) = await _fileRepository.SearchAsync(
            moduleId,
            search,
            page,
            pageSize,
            accessibleModuleIds);

        return (items.ToList(), total);
    }

    public async Task<FileWithDetailsViewModel?> GetFileWithDetailsAsync(int id, User currentUser)
    {
        var file = await _fileRepository.GetWithModuleAsync(id);

        if (file == null)
        {
            return null;
        }

        // Access control check
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this file");
        }

        return new FileWithDetailsViewModel
        {
            File = file,
            ModuleName = file.Module?.Name,
            CourseName = file.Module?.Course?.Name,
            UniversityName = file.Module?.Course?.University?.Name
        };
    }

    public async Task<FileEntity> UploadFileAsync(
        int moduleId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string? customName,
        User currentUser)
    {
        // Check if file is provided
        if (fileStream == null || fileSize == 0)
        {
            throw new InvalidOperationException("File is required");
        }

        // Check if module exists and get with details
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId);
        if (module == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        // Access control: Check if user can upload to this module
        var accessibleModuleIds = await GetAccessibleModuleIdsAsync(currentUser);
        if (!accessibleModuleIds.Contains(moduleId))
        {
            throw new UnauthorizedAccessException("You do not have access to upload files to this module");
        }

        // Validate file size (10MB limit)
        if (fileSize > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException("File size exceeds 10MB limit");
        }

        // Sanitize filename
        var sanitizedFilename = FileHelper.SanitizeFilename(originalFileName);
        if (string.IsNullOrWhiteSpace(sanitizedFilename))
        {
            throw new InvalidOperationException("Invalid filename");
        }

        // Sanitize display name
        var sanitizedName = string.IsNullOrWhiteSpace(customName)
            ? sanitizedFilename
            : FileHelper.SanitizeFilename(customName);

        // Generate blob path
        var blobPath = _blobStorageService.GenerateBlobPath(
            module.Course.UniversityId,
            module.CourseId,
            moduleId,
            sanitizedFilename
        );

        // Upload to blob storage
        var blobUrl = await _blobStorageService.UploadFileAsync(
            fileStream,
            blobPath,
            contentType
        );

        // Create file record
        var fileExtension = Path.GetExtension(sanitizedFilename).TrimStart('.').ToLowerInvariant();
        var fileEntity = new FileEntity
        {
            Name = sanitizedName,
            FileType = string.IsNullOrEmpty(fileExtension) ? "upload" : fileExtension,
            FileName = sanitizedName,
            BlobUrl = blobUrl,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSize = fileSize,
            ModuleId = moduleId,
            IsActive = true,
            ProcessingStatus = "pending"
        };

        var created = await _fileRepository.AddAsync(fileEntity);

        // Audit log: File uploaded
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: module.Course.UniversityId,
            action: "Create",
            entityType: "File",
            entityId: created.Id,
            entityName: created.Name,
            changes: null);

        return created;
    }

    public async Task<FileEntity> UploadProfessorAgentFileAsync(
        int professorAgentId,
        int universityId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string? customName,
        User currentUser)
    {
        if (fileStream == null || fileSize == 0)
            throw new InvalidOperationException("File is required");

        if (fileSize > 10 * 1024 * 1024)
            throw new InvalidOperationException("File size exceeds 10MB limit");

        var sanitizedFilename = FileHelper.SanitizeFilename(originalFileName);
        if (string.IsNullOrWhiteSpace(sanitizedFilename))
            throw new InvalidOperationException("Invalid filename");

        var sanitizedName = string.IsNullOrWhiteSpace(customName)
            ? sanitizedFilename
            : FileHelper.SanitizeFilename(customName);

        var blobPath = $"professor-agents/{professorAgentId}/{sanitizedFilename}";
        var blobUrl = await _blobStorageService.UploadFileAsync(fileStream, blobPath, contentType);

        var fileExtension = Path.GetExtension(sanitizedFilename).TrimStart('.').ToLowerInvariant();
        var fileEntity = new FileEntity
        {
            Name = sanitizedName,
            FileType = string.IsNullOrEmpty(fileExtension) ? "upload" : fileExtension,
            FileName = sanitizedName,
            BlobUrl = blobUrl,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSize = fileSize,
            ModuleId = null,
            ProfessorAgentId = professorAgentId,
            IsActive = true,
            ProcessingStatus = "pending"
        };

        var created = await _fileRepository.AddAsync(fileEntity);

        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: universityId,
            action: "Create",
            entityType: "ProfessorAgentFile",
            entityId: created.Id,
            entityName: created.Name,
            changes: null);

        return created;
    }

    public async Task<List<FileEntity>> GetProfessorAgentFilesAsync(int professorAgentId)
    {
        return await _fileRepository.GetByProfessorAgentIdAsync(professorAgentId);
    }

    public async Task<string> GetDownloadUrlAsync(int id, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Access control
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this file");
        }

        // Generate SAS token for download (1 hour expiry)
        return _blobStorageService.GetDownloadUrl(file.BlobPath ?? file.FileName ?? "", expiresInHours: 1);
    }

    public async Task<FileEntity> UpdateFileAsync(int id, string? newFileName, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Access control
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to update this file");
        }

        // Track changes for audit log
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>();
        var oldFileName = file.FileName;

        // Update filename if provided
        if (!string.IsNullOrWhiteSpace(newFileName))
        {
            var sanitizedNewName = FileHelper.SanitizeFilename(newFileName);
            if (file.FileName != sanitizedNewName)
            {
                changes["FileName"] = (file.FileName, sanitizedNewName);
                file.FileName = sanitizedNewName;
            }
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _fileRepository.UpdateAsync(file);

        // Get module and course to retrieve university ID for audit log
        if (changes.Any())
        {
            var module = file.ModuleId.HasValue ? await _moduleRepository.GetByIdAsync(file.ModuleId.Value) : null;
            var course = module != null ? await _courseRepository.GetByIdAsync(module.CourseId) : null;

            // Audit log: Only log if there were actual changes
            await _auditLogService.LogAsync(
                userId: currentUser.UserId,
                username: currentUser.Username,
                universityId: course?.UniversityId,
                action: "Update",
                entityType: "File",
                entityId: file.Id,
                entityName: file.Name,
                changes: changes);
        }

        return file;
    }

    public async Task<FileEntity> UpdateFileStatusAsync(
        int id,
        string status,
        string? errorMessage,
        string? openAIFileId)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Map status to IsActive (active/completed = true, failed/pending = false)
        file.IsActive = status == "completed" || status == "active";

        // Note: ErrorMessage property removed from schema - errors are logged elsewhere

        if (!string.IsNullOrWhiteSpace(openAIFileId))
        {
            file.OpenAIFileId = openAIFileId;
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _fileRepository.UpdateAsync(file);
        return file;
    }

    public async Task DeleteFileAsync(int id, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Access control
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to delete this file");
        }

        // Get module and course to retrieve university ID for audit log before deletion
        var module = file.ModuleId.HasValue ? await _moduleRepository.GetByIdAsync(file.ModuleId.Value) : null;
        var course = module != null ? await _courseRepository.GetByIdAsync(module.CourseId) : null;

        // Delete from blob storage
        await _blobStorageService.DeleteFileAsync(file.BlobPath ?? file.FileName ?? "");

        // Delete from database
        await _fileRepository.DeleteAsync(file);

        // Audit log: File deleted
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: course?.UniversityId,
            action: "Delete",
            entityType: "File",
            entityId: file.Id,
            entityName: file.Name,
            changes: null);
    }

    public async Task<bool> CanUserAccessFileAsync(int fileId, User user)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }

        // Professor agent files have no module — access granted to any authenticated user
        // (route scoping /{agentId}/files/{fileId} provides the real constraint)
        if (file.ProfessorAgentId.HasValue && !file.ModuleId.HasValue)
        {
            return true;
        }

        var accessibleModuleIds = await GetAccessibleModuleIdsAsync(user);
        return file.ModuleId.HasValue && accessibleModuleIds.Contains(file.ModuleId.Value);
    }
}
