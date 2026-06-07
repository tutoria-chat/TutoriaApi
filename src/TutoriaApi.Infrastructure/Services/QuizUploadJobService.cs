using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class QuizUploadJobService : IQuizUploadJobService
{
    private readonly IQuizUploadJobRepository _quizUploadJobRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly ILogger<QuizUploadJobService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".csv", ".txt"
    };

    public QuizUploadJobService(
        IQuizUploadJobRepository quizUploadJobRepository,
        IModuleRepository moduleRepository,
        IBlobStorageService blobStorageService,
        ISqsMessagingService sqsMessagingService,
        ILogger<QuizUploadJobService> logger)
    {
        _quizUploadJobRepository = quizUploadJobRepository;
        _moduleRepository = moduleRepository;
        _blobStorageService = blobStorageService;
        _sqsMessagingService = sqsMessagingService;
        _logger = logger;
    }

    public async Task<QuizUploadJob> CreateJobAsync(int moduleId, Stream fileStream, string fileName, string contentType, User currentUser)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        // University-scope check: non-super-admins must belong to this university
        if (currentUser.UserType != UserTypes.SuperAdmin
            && currentUser.UniversityId.HasValue
            && currentUser.UniversityId.Value != module.Course?.UniversityId)
        {
            throw new UnauthorizedAccessException("You do not have access to this module");
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not supported. Allowed: PDF, DOCX, XLSX, CSV, TXT");

        // Create job record first so we have the ID for the S3 path
        var job = new QuizUploadJob
        {
            ModuleId = moduleId,
            Status = "pending",
            ExtractedCount = 0,
            OriginalFilename = fileName
        };

        var created = await _quizUploadJobRepository.AddAsync(job);

        // Upload file to S3: quiz-upload-jobs/{moduleId}/{jobId}/{filename}
        var s3Key = $"quiz-upload-jobs/{moduleId}/{created.Id}/{fileName}";
        try
        {
            await _blobStorageService.UploadFileAsync(fileStream, s3Key, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload quiz file to S3 for job {JobId}", created.Id);
            created.Status = "failed";
            created.ErrorMessage = "Failed to upload file";
            await _quizUploadJobRepository.UpdateAsync(created);
            throw;
        }

        created.InputS3Key = s3Key;
        await _quizUploadJobRepository.UpdateAsync(created);

        // Enqueue SQS message — worker downloads and extracts questions asynchronously
        var sent = await _sqsMessagingService.SendQuizUploadJobAsync(created.Id, moduleId);
        if (!sent)
        {
            _logger.LogWarning(
                "SQS message not sent for quiz upload job {JobId} — job created but may not process automatically",
                created.Id);
        }

        _logger.LogInformation(
            "Created quiz upload job {JobId} for module {ModuleId} by user {UserId}",
            created.Id, moduleId, currentUser.UserId);

        return created;
    }

    public async Task<List<QuizUploadJob>> GetJobsForModuleAsync(int moduleId, User currentUser)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found");

        if (currentUser.UserType != UserTypes.SuperAdmin
            && currentUser.UniversityId.HasValue
            && currentUser.UniversityId.Value != module.Course?.UniversityId)
        {
            throw new UnauthorizedAccessException("You do not have access to this module");
        }

        var jobs = await _quizUploadJobRepository.GetByModuleIdAsync(moduleId);
        return jobs.OrderByDescending(j => j.CreatedAt).ToList();
    }

    public async Task<QuizUploadJob?> GetJobWithQuestionsAsync(int jobId, User currentUser)
    {
        var job = await _quizUploadJobRepository.GetWithModuleAsync(jobId);
        if (job == null) return null;

        // For non-super-admins, verify the job's module belongs to their university
        // by loading the module with course details
        if (currentUser.UserType != UserTypes.SuperAdmin && currentUser.UniversityId.HasValue)
        {
            var module = await _moduleRepository.GetWithDetailsAsync(job.ModuleId);
            if (module?.Course?.UniversityId != currentUser.UniversityId.Value)
                throw new UnauthorizedAccessException("You do not have access to this job");
        }

        return job;
    }
}
