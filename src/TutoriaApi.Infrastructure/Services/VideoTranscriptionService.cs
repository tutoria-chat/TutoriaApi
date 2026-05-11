using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

public class VideoTranscriptionService : IVideoTranscriptionService
{
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly IFileRepository _fileRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<VideoTranscriptionService> _logger;

    public VideoTranscriptionService(
        ISqsMessagingService sqsMessagingService,
        IFileRepository fileRepository,
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        ILogger<VideoTranscriptionService> logger)
    {
        _sqsMessagingService = sqsMessagingService;
        _fileRepository = fileRepository;
        _moduleRepository = moduleRepository;
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<FileEntity> TranscribeYoutubeVideoAsync(
        string youtubeUrl,
        int moduleId,
        string language,
        string? customName,
        User currentUser)
    {
        // Verify module exists and user has access (with eager loading to avoid N+1 queries)
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId);
        if (module == null)
        {
            throw new KeyNotFoundException($"Module {moduleId} not found");
        }

        // Authorization check (Course is already loaded via GetWithDetailsAsync)
        if (!await CanAccessModuleAsync(module, currentUser))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this module");
        }

        _logger.LogInformation(
            "User {UserId} submitting YouTube video for transcription: {Url}, Module: {ModuleId}",
            currentUser.UserId,
            youtubeUrl,
            moduleId);

        // Create a pending File record — the worker will fill in the transcript
        var pendingFile = new FileEntity
        {
            Name = customName ?? $"YouTube Video",
            FileName = customName ?? $"YouTube Video",
            FileType = "video/youtube",
            ModuleId = moduleId,
            SourceType = "youtube",
            SourceUrl = youtubeUrl,
            TranscriptionStatus = "pending",
            TranscriptLanguage = language,
            ProcessingStatus = "pending",
            IsActive = true,
        };

        var file = await _fileRepository.AddAsync(pendingFile);

        _logger.LogInformation(
            "Created pending file record {FileId} for YouTube video, queuing transcription job",
            file.Id);

        await _sqsMessagingService.SendTranscriptionJobAsync(file.Id, youtubeUrl, language);

        return file;
    }

    public async Task<FileEntity?> GetTranscriptionStatusAsync(int fileId, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return null;
        }

        // Authorization check (with eager loading to avoid N+1 queries)
        var module = file.ModuleId.HasValue ? await _moduleRepository.GetWithDetailsAsync(file.ModuleId.Value) : null;
        if (module == null || !await CanAccessModuleAsync(module, currentUser))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this file");
        }

        return file;
    }

    public async Task<FileEntity?> GetTranscriptTextAsync(int fileId, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return null;
        }

        // Authorization check (with eager loading to avoid N+1 queries)
        var module = file.ModuleId.HasValue ? await _moduleRepository.GetWithDetailsAsync(file.ModuleId.Value) : null;
        if (module == null || !await CanAccessModuleAsync(module, currentUser))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this file");
        }

        if (string.IsNullOrEmpty(file.TranscriptText))
        {
            return null;
        }

        return file;
    }

    public async Task<FileEntity> RetryTranscriptionAsync(int fileId, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            throw new KeyNotFoundException($"File {fileId} not found");
        }

        // Authorization check (with eager loading to avoid N+1 queries)
        var module = file.ModuleId.HasValue ? await _moduleRepository.GetWithDetailsAsync(file.ModuleId.Value) : null;
        if (module == null || !await CanAccessModuleAsync(module, currentUser))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this file");
        }

        if (file.TranscriptionStatus != "failed")
        {
            throw new InvalidOperationException("Only failed transcriptions can be retried");
        }

        _logger.LogInformation(
            "User {UserId} retrying transcription for file {FileId}",
            currentUser.UserId,
            fileId);

        // Reset status and re-queue via SQS
        file.TranscriptionStatus = "pending";
        await _fileRepository.UpdateAsync(file);

        await _sqsMessagingService.SendTranscriptionJobAsync(file.Id, file.SourceUrl!, file.TranscriptLanguage ?? "pt-br");

        return file;
    }

    public async Task<bool> DeleteTranscriptionAsync(int fileId, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }

        // Authorization check (with eager loading to avoid N+1 queries)
        var module = file.ModuleId.HasValue ? await _moduleRepository.GetWithDetailsAsync(file.ModuleId.Value) : null;
        if (module == null || !await CanAccessModuleAsync(module, currentUser))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this file");
        }

        _logger.LogInformation(
            "User {UserId} deleting transcription file {FileId}",
            currentUser.UserId,
            fileId);

        // Soft delete
        file.IsActive = false;
        await _fileRepository.UpdateAsync(file);

        return true;
    }

    private async Task<bool> CanAccessModuleAsync(Module module, User currentUser)
    {
        // Super admins can access everything
        if (currentUser.UserType == UserTypes.SuperAdmin)
        {
            return true;
        }

        // Manager, Tutor, Platform Coordinator must be in same university
        if (currentUser.UserType == UserTypes.Manager ||
            currentUser.UserType == UserTypes.Tutor ||
            currentUser.UserType == UserTypes.PlatformCoordinator)
        {
            if (currentUser.UniversityId == null)
            {
                return false;
            }

            var course = module.Course ?? await _courseRepository.GetByIdAsync(module.CourseId);
            if (course == null)
            {
                return false;
            }

            // University-scoped roles can access all modules in their university
            return course.UniversityId == currentUser.UniversityId;
        }

        // Legacy: Support old professor with isAdmin flag
        if (currentUser.UserType == UserTypes.Professor)
        {
            if (currentUser.UniversityId == null)
            {
                return false;
            }

            var course = module.Course ?? await _courseRepository.GetByIdAsync(module.CourseId);
            if (course == null)
            {
                return false;
            }

            if (course.UniversityId != currentUser.UniversityId)
            {
                return false;
            }

            // Admin professors can access all in their university
            if (currentUser.IsAdmin == true)
            {
                return true;
            }

            // Regular professors need to be assigned to the course
            // This would require checking ProfessorCourses table
            // For now, allow if same university
            return true;
        }

        return false;
    }
}
