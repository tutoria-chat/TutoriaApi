using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class CalendarImportJobService : ICalendarImportJobService
{
    private readonly ICalendarImportJobRepository _jobRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseEventService _courseEventService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CalendarImportJobService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".csv", ".txt"
    };

    public CalendarImportJobService(
        ICalendarImportJobRepository jobRepository,
        ICourseRepository courseRepository,
        ICourseEventService courseEventService,
        IBlobStorageService blobStorageService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CalendarImportJobService> logger)
    {
        _jobRepository = jobRepository;
        _courseRepository = courseRepository;
        _courseEventService = courseEventService;
        _blobStorageService = blobStorageService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<CalendarImportJob> CreateJobAsync(int courseId, Stream fileStream, string fileName, string contentType, User currentUser)
    {
        await EnsureCourseAccessAsync(courseId, currentUser);

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not supported. Allowed: PDF, DOCX, XLSX, CSV, TXT");

        var job = new CalendarImportJob
        {
            CourseId = courseId,
            Status = "pending",
            OriginalFilename = fileName,
            CreatedByUserId = currentUser.UserId,
        };
        var created = await _jobRepository.AddAsync(job);

        // Store the file so the worker (and any reprocess) can read it.
        var s3Key = $"calendar-import-jobs/{courseId}/{created.Id}/{fileName}";
        try
        {
            await _blobStorageService.UploadFileAsync(fileStream, s3Key, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload calendar import file to S3 for job {JobId}", created.Id);
            created.Status = "failed";
            created.ErrorMessage = "Failed to upload file";
            await _jobRepository.UpdateAsync(created);
            return created;
        }

        created.InputS3Key = s3Key;
        await _jobRepository.UpdateAsync(created);

        // Extraction lives on the worker (always-on); trigger it and wait for the
        // result. The worker writes status/ExtractedEventsJson back to this row.
        try
        {
            await TriggerWorkerExtractionAsync(created.Id, courseId, s3Key, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker extraction call failed for calendar import job {JobId}", created.Id);
            created.Status = "failed";
            created.ErrorMessage = "Extraction service is unavailable. Please try again.";
            created.ProcessedAt = DateTime.UtcNow;
            await _jobRepository.UpdateAsync(created);
            return created;
        }

        // Reload the row the worker just updated.
        return await _jobRepository.GetWithCourseAsync(created.Id) ?? created;
    }

    public async Task<List<CalendarImportJob>> GetJobsForCourseAsync(int courseId, User currentUser)
    {
        await EnsureCourseAccessAsync(courseId, currentUser);
        var jobs = await _jobRepository.GetByCourseIdAsync(courseId);
        return jobs.ToList();
    }

    public async Task<CalendarImportJob?> GetJobAsync(int jobId, User currentUser)
    {
        var job = await _jobRepository.GetWithCourseAsync(jobId);
        if (job == null) return null;
        await EnsureCourseAccessAsync(job.CourseId, currentUser);
        return job;
    }

    public async Task<int> ConfirmAsync(int jobId, List<CourseEvent> events, User currentUser)
    {
        var job = await _jobRepository.GetWithCourseAsync(jobId)
            ?? throw new KeyNotFoundException($"Calendar import job {jobId} not found");
        await EnsureCourseAccessAsync(job.CourseId, currentUser);

        var created = 0;
        foreach (var ev in events)
        {
            ev.CourseId = job.CourseId;       // never trust a client-supplied course
            ev.AssignmentId = null;           // imported events are standalone
            await _courseEventService.CreateAsync(ev, currentUser);
            created++;
        }

        job.Status = "confirmed";
        job.ConfirmedCount = created;
        job.ConfirmedAt = DateTime.UtcNow;
        await _jobRepository.UpdateAsync(job);

        _logger.LogInformation("Confirmed {Count} calendar events from import job {JobId}", created, jobId);
        return created;
    }

    private async Task TriggerWorkerExtractionAsync(int jobId, int courseId, string s3Key, string fileName)
    {
        var baseUrl = _configuration["WorkerApi:BaseUrl"]?.TrimEnd('/');
        var apiKey = _configuration["WorkerApi:InternalApiKey"] ?? _configuration["AiApi:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("WorkerApi:BaseUrl is not configured");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Internal API key is not configured");

        using var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(120);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v2/calendar/process");
        req.Headers.Add("X-Internal-Api-Key", apiKey);
        req.Content = JsonContent.Create(new
        {
            job_id = jobId,
            course_id = courseId,
            s3_key = s3Key,
            filename = fileName,
        });

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<WorkerExtractResult>();
        _logger.LogInformation(
            "Calendar extraction for job {JobId}: status={Status} events={Count}",
            jobId, result?.Status, result?.ExtractedCount);
    }

    private async Task EnsureCourseAccessAsync(int courseId, User currentUser)
    {
        if (currentUser.UserType == UserTypes.SuperAdmin) return;

        var course = await _courseRepository.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found");

        if (UserTypes.UniversityStaffRoles.Contains(currentUser.UserType))
        {
            if (course.UniversityId != currentUser.UniversityId)
                throw new UnauthorizedAccessException("Access denied: course belongs to a different university");
            return;
        }

        throw new UnauthorizedAccessException("Access denied");
    }

    private class WorkerExtractResult
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("extracted_count")] public int ExtractedCount { get; set; }
    }
}
