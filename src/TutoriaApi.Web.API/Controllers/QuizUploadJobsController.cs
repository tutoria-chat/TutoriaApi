using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Async quiz file upload — professors upload a file, worker extracts questions in the background.
/// </summary>
[ApiController]
[Route("api/quiz-upload-jobs")]
[Authorize(Policy = "ProfessorOrAbove")]
public class QuizUploadJobsController : ControllerBase
{
    private readonly IQuizUploadJobService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QuizUploadJobsController> _logger;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public QuizUploadJobsController(
        IQuizUploadJobService service,
        ICurrentUserService currentUserService,
        ILogger<QuizUploadJobsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a quiz question file and queue it for async extraction.
    /// </summary>
    /// <remarks>Returns 202 immediately. Poll GET /api/quiz-upload-jobs?moduleId={id} for status.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(QuizUploadJobDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<QuizUploadJobDto>> CreateQuizUploadJob([FromForm] CreateQuizUploadJobRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "File is required" });

        if (request.File.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File exceeds the 10 MB limit" });

        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            await using var stream = request.File.OpenReadStream();
            var job = await _service.CreateJobAsync(
                request.ModuleId,
                stream,
                request.File.FileName,
                request.File.ContentType ?? "application/octet-stream",
                currentUser);

            return StatusCode(StatusCodes.Status202Accepted, MapToDto(job));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quiz upload job for module {ModuleId}", request.ModuleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// List quiz upload jobs for a module, newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<QuizUploadJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<QuizUploadJobDto>>> GetJobs([FromQuery] int moduleId)
    {
        if (moduleId <= 0)
            return BadRequest(new { message = "moduleId is required" });

        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var jobs = await _service.GetJobsForModuleAsync(moduleId, currentUser);
            return Ok(jobs.Select(MapToDto).ToList());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing quiz upload jobs for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get extracted questions for a completed quiz upload job.
    /// </summary>
    [HttpGet("{id}/questions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetQuestions(int id)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var job = await _service.GetJobWithQuestionsAsync(id, currentUser);

            if (job == null)
                return NotFound(new { message = "Quiz upload job not found" });

            if (job.Status != "completed")
                return BadRequest(new { message = $"Job is not completed yet (status: {job.Status})" });

            // Parse stored JSON and return as-is (worker stored it, UI expects the same shape)
            object? questions = null;
            if (!string.IsNullOrEmpty(job.ExtractedQuestionsJson))
            {
                questions = System.Text.Json.JsonSerializer.Deserialize<object>(job.ExtractedQuestionsJson);
            }

            return Ok(new
            {
                jobId = job.Id,
                moduleId = job.ModuleId,
                status = job.Status,
                extractedCount = job.ExtractedCount,
                questions = questions ?? Array.Empty<object>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting questions for quiz upload job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static QuizUploadJobDto MapToDto(Core.Entities.QuizUploadJob job) => new()
    {
        Id = job.Id,
        ModuleId = job.ModuleId,
        Status = job.Status,
        ExtractedCount = job.ExtractedCount,
        ErrorMessage = job.ErrorMessage,
        OriginalFilename = job.OriginalFilename,
        ProcessedAt = job.ProcessedAt,
        CreatedAt = job.CreatedAt
    };
}
