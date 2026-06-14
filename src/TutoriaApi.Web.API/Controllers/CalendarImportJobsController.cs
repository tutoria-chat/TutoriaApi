using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Import course calendar events from an uploaded PDF: upload → AI extraction →
/// professor review → confirm (creates CourseEvents).
/// </summary>
[ApiController]
[Route("api/calendar-import-jobs")]
[Authorize(Policy = "ProfessorOrAbove")]
public class CalendarImportJobsController : ControllerBase
{
    private readonly ICalendarImportJobService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CalendarImportJobsController> _logger;

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CalendarImportJobsController(
        ICalendarImportJobService service,
        ICurrentUserService currentUserService,
        ILogger<CalendarImportJobsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>Upload a file; the worker extracts candidate events for review.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CalendarImportJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarImportJobDto>> Create([FromForm] CreateCalendarImportJobRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "File is required" });
        if (request.File.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File exceeds the 10 MB limit" });

        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            await using var stream = request.File.OpenReadStream();
            var job = await _service.CreateJobAsync(
                request.CourseId,
                stream,
                request.File.FileName,
                request.File.ContentType ?? "application/octet-stream",
                currentUser);

            return Ok(MapToDto(job));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating calendar import job for course {CourseId}", request.CourseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Import from an external calendar's iCal feed URL (Google/Outlook/Apple).</summary>
    [HttpPost("from-url")]
    [ProducesResponseType(typeof(CalendarImportJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarImportJobDto>> CreateFromUrl([FromBody] CreateCalendarImportFromUrlRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var job = await _service.CreateJobFromUrlAsync(request.CourseId, request.SourceUrl, currentUser);
            return Ok(MapToDto(job));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating calendar import (url) for course {CourseId}", request.CourseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>List import jobs for a course, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CalendarImportJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CalendarImportJobDto>>> GetJobs([FromQuery] int courseId)
    {
        if (courseId <= 0) return BadRequest(new { message = "courseId is required" });
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var jobs = await _service.GetJobsForCourseAsync(courseId, currentUser);
            return Ok(jobs.Select(MapToDto).ToList());
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing calendar import jobs for course {CourseId}", courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Get a single job with its extracted events (for the review screen).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CalendarImportJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarImportJobDto>> GetJob(int id)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var job = await _service.GetJobAsync(id, currentUser);
            if (job == null) return NotFound(new { message = "Calendar import job not found" });
            return Ok(MapToDto(job));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar import job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Confirm the reviewed events — creates CourseEvents.</summary>
    [HttpPost("{id}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Confirm(int id, [FromBody] ConfirmCalendarImportRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var events = request.Events.Select(e => new CourseEvent
            {
                Title = e.Title,
                Description = e.Description,
                EventType = e.EventType,
                StartsAtUtc = e.StartsAtUtc,
                EndsAtUtc = e.EndsAtUtc,
                ModuleId = e.ModuleId,
                Remind7Days = e.Remind7Days,
                Remind3Days = e.Remind3Days,
                Remind2Days = e.Remind2Days,
                Remind24Hours = e.Remind24Hours,
            }).ToList();

            var created = await _service.ConfirmAsync(id, events, currentUser);
            return Ok(new { status = "ok", created });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming calendar import job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static CalendarImportJobDto MapToDto(CalendarImportJob job)
    {
        var events = new List<ExtractedCalendarEventDto>();
        if (!string.IsNullOrEmpty(job.ExtractedEventsJson))
        {
            try
            {
                events = JsonSerializer.Deserialize<List<ExtractedCalendarEventDto>>(job.ExtractedEventsJson, JsonOpts)
                         ?? new List<ExtractedCalendarEventDto>();
            }
            catch { /* malformed stored JSON — return empty list */ }
        }

        return new CalendarImportJobDto
        {
            Id = job.Id,
            CourseId = job.CourseId,
            Status = job.Status,
            ExtractedCount = job.ExtractedCount,
            ConfirmedCount = job.ConfirmedCount,
            ErrorMessage = job.ErrorMessage,
            OriginalFilename = job.OriginalFilename,
            ProcessedAt = job.ProcessedAt,
            ConfirmedAt = job.ConfirmedAt,
            CreatedAt = job.CreatedAt ?? default,
            Events = events,
        };
    }
}
