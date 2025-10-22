using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

public class VideoTranscriptionService : IVideoTranscriptionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IFileRepository _fileRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<VideoTranscriptionService> _logger;

    public VideoTranscriptionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IFileRepository fileRepository,
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        ILogger<VideoTranscriptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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

        // Call AI API
        var aiApiUrl = _configuration["AiApi:BaseUrl"] ?? "http://localhost:8000";
        var httpClient = _httpClientFactory.CreateClient();

        var payload = new
        {
            youtube_url = youtubeUrl,
            module_id = moduleId,
            language = language,
            custom_name = customName
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync(
            $"{aiApiUrl}/api/v2/transcription/youtube",
            jsonContent);

        // Handle both 201 (sync completion - legacy) and 202 (async processing)
        if (response.StatusCode != System.Net.HttpStatusCode.Created &&
            response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "AI API returned error {StatusCode}: {Error}",
                response.StatusCode,
                errorContent);

            throw new InvalidOperationException($"Transcription service returned error: {errorContent}");
        }

        var resultJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        var fileId = result.GetProperty("file_id").GetInt32();
        var status = result.GetProperty("status").GetString();

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            _logger.LogInformation(
                "YouTube transcription queued for processing: File {FileId}, Status: {Status}",
                fileId,
                status);
        }
        else
        {
            _logger.LogInformation(
                "YouTube transcription completed: File {FileId}, Source: {Source}",
                fileId,
                result.GetProperty("source").GetString());
        }

        // Get the created file from database
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            throw new InvalidOperationException("File was created but not found in database");
        }

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
        var module = await _moduleRepository.GetWithDetailsAsync(file.ModuleId);
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
        var module = await _moduleRepository.GetWithDetailsAsync(file.ModuleId);
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
        var module = await _moduleRepository.GetWithDetailsAsync(file.ModuleId);
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

        // Call AI API retry endpoint
        var aiApiUrl = _configuration["AiApi:BaseUrl"] ?? "http://localhost:8000";
        var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient.PostAsync(
            $"{aiApiUrl}/api/v2/transcription/retry/{fileId}",
            null);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "AI API returned error {StatusCode}: {Error}",
                response.StatusCode,
                errorContent);

            throw new InvalidOperationException($"Failed to retry transcription: {errorContent}");
        }

        // Refresh file from database
        var updatedFile = await _fileRepository.GetByIdAsync(fileId);
        if (updatedFile == null)
        {
            throw new InvalidOperationException("File disappeared during retry");
        }

        return updatedFile;
    }

    public async Task<bool> DeleteTranscriptionAsync(int fileId, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }

        // Authorization check (with eager loading to avoid N+1 queries)
        var module = await _moduleRepository.GetWithDetailsAsync(file.ModuleId);
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
        if (currentUser.UserType == "super_admin")
        {
            return true;
        }

        // Professors must be in same university
        if (currentUser.UserType == "professor")
        {
            if (currentUser.UniversityId == null)
            {
                return false;
            }

            // Use Course navigation property (should be loaded via GetWithDetailsAsync)
            // If not loaded, fall back to repository call
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
