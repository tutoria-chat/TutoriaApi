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

    public FileService(
        IFileRepository fileRepository,
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        IBlobStorageService blobStorageService,
        AccessControlHelper accessControl)
    {
        _fileRepository = fileRepository;
        _moduleRepository = moduleRepository;
        _courseRepository = courseRepository;
        _blobStorageService = blobStorageService;
        _accessControl = accessControl;
    }

    public async Task<List<int>> GetAccessibleModuleIdsAsync(User user)
    {
        if (user.UserType == "super_admin")
        {
            // Super admins can access all modules
            var allModules = await _moduleRepository.GetAllAsync();
            return allModules.Select(m => m.Id).ToList();
        }

        if (user.UserType == "professor")
        {
            if (user.IsAdmin ?? false)
            {
                // Admin professors can access all modules in their university
                var universityModules = await _moduleRepository.GetByUniversityIdAsync(user.UniversityId ?? 0);
                return universityModules.Select(m => m.Id).ToList();
            }
            else
            {
                // Regular professors can only access modules from assigned courses
                var courseIds = await _accessControl.GetProfessorCourseIdsAsync(user.UserId);
                var modules = new List<int>();
                foreach (var courseId in courseIds)
                {
                    var courseModules = await _moduleRepository.GetByCourseIdAsync(courseId);
                    modules.AddRange(courseModules.Select(m => m.Id));
                }
                return modules;
            }
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

        // Validate file size (50MB limit)
        if (fileSize > 50 * 1024 * 1024)
        {
            throw new InvalidOperationException("File size exceeds 50MB limit");
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
        var fileEntity = new FileEntity
        {
            Name = sanitizedName,
            FileType = "upload", // Default file type
            FileName = sanitizedName,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSize = fileSize,
            ModuleId = moduleId,
            IsActive = true
        };

        return await _fileRepository.AddAsync(fileEntity);
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

        // Update filename if provided
        if (!string.IsNullOrWhiteSpace(newFileName))
        {
            file.FileName = FileHelper.SanitizeFilename(newFileName);
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _fileRepository.UpdateAsync(file);
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

        // Delete from blob storage
        await _blobStorageService.DeleteFileAsync(file.BlobPath ?? file.FileName ?? "");

        // Delete from database
        await _fileRepository.DeleteAsync(file);
    }

    public async Task<bool> CanUserAccessFileAsync(int fileId, User user)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }

        var accessibleModuleIds = await GetAccessibleModuleIdsAsync(user);
        return accessibleModuleIds.Contains(file.ModuleId);
    }
}
