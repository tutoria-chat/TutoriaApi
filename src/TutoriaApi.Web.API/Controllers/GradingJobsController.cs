using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// AI-powered batch grading of written test answers.
/// </summary>
/// <remarks>
/// Professors upload a JSON file containing student answers (Moodle-compatible format).
/// A background worker grades each answer using AI + course context and produces a downloadable CSV.
///
/// **Authorization**: Requires ProfessorOrAbove. University must have HasAssignments = true.
/// </remarks>
[ApiController]
[Route("api/grading-jobs")]
[Authorize(Policy = "ProfessorOrAbove")]
public class GradingJobsController : ControllerBase
{
    private readonly IGradingJobService _gradingJobService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GradingJobsController> _logger;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public GradingJobsController(
        IGradingJobService gradingJobService,
        ICurrentUserService currentUserService,
        ILogger<GradingJobsController> logger)
    {
        _gradingJobService = gradingJobService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a student-answers JSON and create a grading job.
    /// </summary>
    /// <remarks>
    /// Accepts a JSON file (max 10 MB). Returns 202 Accepted immediately — grading happens asynchronously.
    /// Poll GET /api/grading-jobs?courseId={id} to check status.
    /// </remarks>
    /// <response code="202">Job created and queued for processing.</response>
    /// <response code="400">Invalid file type, file too large, or missing courseId.</response>
    /// <response code="403">University does not have the grading feature enabled.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GradingJobDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<GradingJobDto>> CreateGradingJob([FromForm] CreateGradingJobRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "File is required" });

        var ext = Path.GetExtension(request.File.FileName)?.ToLowerInvariant();
        if (ext != ".json")
            return BadRequest(new { message = "Only .json files are accepted" });

        if (request.File.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File exceeds the 10 MB limit" });

        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            await using var stream = request.File.OpenReadStream();
            var job = await _gradingJobService.CreateJobAsync(
                request.CourseId,
                stream,
                request.File.FileName,
                currentUser,
                request.GradingCriteria);

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
            _logger.LogError(ex, "Error creating grading job for course {CourseId}", request.CourseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// List grading jobs for a course, newest first.
    /// </summary>
    /// <param name="courseId">The course ID to list jobs for.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<GradingJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GradingJobDto>>> GetGradingJobs([FromQuery] int courseId)
    {
        if (courseId <= 0)
            return BadRequest(new { message = "courseId is required" });

        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var jobs = await _gradingJobService.GetJobsForCourseAsync(courseId, currentUser);
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
            _logger.LogError(ex, "Error listing grading jobs for course {CourseId}", courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get a 24-hour pre-signed download URL for a completed grading CSV.
    /// </summary>
    /// <param name="id">The grading job ID.</param>
    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDownloadUrl(int id)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var downloadUrl = await _gradingJobService.GetDownloadUrlAsync(id, currentUser);
            return Ok(new { downloadUrl });
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
            _logger.LogError(ex, "Error getting download URL for grading job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static GradingJobDto MapToDto(Core.Entities.GradingJob job) => new()
    {
        Id = job.Id,
        CourseId = job.CourseId,
        Status = job.Status,
        TotalSubmissions = job.TotalSubmissions,
        ProcessedSubmissions = job.ProcessedSubmissions,
        ErrorMessage = job.ErrorMessage,
        ProcessedAt = job.ProcessedAt,
        HasResult = !string.IsNullOrEmpty(job.ResultS3Key),
        GradingCriteria = job.GradingCriteria,
        OriginalFilename = job.OriginalFilename,
        CreatedByName = job.CreatedBy is { } u
            ? $"{u.FirstName} {u.LastName}".Trim().TrimEnd()
            : null,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };
}
