using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Auth;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// External automation API for grading — authenticated by the <c>X-Api-Key</c> header
/// (not JWT). Lets an LMS/Moodle integration submit answers for grading and poll for the
/// resulting CSV, matching courses by their external (LMS) course id + university id.
/// </summary>
[ApiController]
[Route("api/external/grading")]
[ExternalApiKey]
public class ExternalGradingController : ControllerBase
{
    private readonly IGradingJobService _gradingService;
    private readonly ILogger<ExternalGradingController> _logger;

    private const long MaxBodyBytes = 10 * 1024 * 1024; // 10 MB

    public ExternalGradingController(IGradingJobService gradingService, ILogger<ExternalGradingController> logger)
    {
        _gradingService = gradingService;
        _logger = logger;
    }

    /// <summary>
    /// Start a grading job. Body = the submissions JSON (same shape as the dashboard upload).
    /// Returns 202 with the job id to poll.
    /// </summary>
    [HttpPost("{universityId:int}/{externalCourseId:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> StartGrading(
        int universityId,
        int externalCourseId,
        [FromQuery] string? criteria = null,
        [FromQuery] string? filename = null)
    {
        try
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, HttpContext.RequestAborted);
            if (ms.Length == 0)
                return BadRequest(new { message = "Request body (submissions JSON) is required" });
            if (ms.Length > MaxBodyBytes)
                return BadRequest(new { message = "Submissions JSON exceeds the 10 MB limit" });
            ms.Position = 0;

            var job = await _gradingService.CreateExternalJobAsync(
                universityId, externalCourseId, ms, filename, criteria);

            return Accepted(new
            {
                jobId = job.Id,
                status = job.Status,
                message = "Grading started. Poll the GET endpoint with this jobId until the CSV is ready.",
            });
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
            _logger.LogError(ex, "External grading start failed (university {U}, externalCourse {C})", universityId, externalCourseId);
            return StatusCode(500, new { message = "An error occurred while starting grading" });
        }
    }

    /// <summary>
    /// Poll a grading job. While it's still grading, returns 202 with a status message.
    /// When complete, returns the grading CSV. Scoped to the course (external id + university).
    /// </summary>
    [HttpGet("{universityId:int}/{externalCourseId:int}/{jobId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> GetResult(int universityId, int externalCourseId, int jobId)
    {
        try
        {
            var job = await _gradingService.GetExternalJobAsync(universityId, externalCourseId, jobId);
            if (job == null)
                return NotFound(new { message = "Grading job not found for this course" });

            if (job.Status == "failed")
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new
                {
                    status = "failed",
                    message = job.ErrorMessage ?? "Grading failed.",
                });

            if (job.Status != "completed")
                return Accepted(new
                {
                    status = job.Status, // pending | processing
                    message = "Still grading — try again soon.",
                    processed = job.ProcessedSubmissions,
                    total = job.TotalSubmissions,
                });

            var bytes = await _gradingService.GetResultBytesAsync(job);
            if (bytes == null)
                return Accepted(new { status = "processing", message = "Still grading — try again soon." });

            return File(bytes, "text/csv", $"grading-{externalCourseId}-{jobId}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External grading poll failed (job {JobId})", jobId);
            return StatusCode(500, new { message = "An error occurred while fetching the grading result" });
        }
    }
}
