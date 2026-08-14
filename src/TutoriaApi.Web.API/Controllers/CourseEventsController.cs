using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Course calendar: tests, assignment due dates, holidays, field events.
/// Published assignments without an explicit event appear as synthesized
/// (read-only) entries; reminder emails are driven by the Remind* flags.
/// </summary>
[ApiController]
[Route("api/course-events")]
[Authorize(Policy = "ProfessorOrAbove")]
public class CourseEventsController : ControllerBase
{
    private readonly ICourseEventService _courseEventService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CourseEventsController> _logger;

    public CourseEventsController(
        ICourseEventService courseEventService,
        ICurrentUserService currentUserService,
        ILogger<CourseEventsController> logger)
    {
        _courseEventService = courseEventService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private static CourseEventDto MapToDto(CourseEvent e) => new()
    {
        Id = e.Id,
        CourseId = e.CourseId,
        ModuleId = e.ModuleId,
        ModuleName = e.Module?.Name,
        AssignmentId = e.AssignmentId,
        Title = e.Title,
        Description = e.Description,
        EventType = e.EventType,
        StartsAtUtc = e.StartsAtUtc,
        EndsAtUtc = e.EndsAtUtc,
        Remind7Days = e.Remind7Days,
        Remind3Days = e.Remind3Days,
        Remind2Days = e.Remind2Days,
        Remind24Hours = e.Remind24Hours,
        IsSynthesized = false,
    };

    private static CourseEventDto MapAssignmentToDto(Assignment a, int courseId) => new()
    {
        Id = null,
        CourseId = courseId,
        // Assignments are course-wide — they no longer belong to a single module
        ModuleId = null,
        ModuleName = null,
        AssignmentId = a.Id,
        Title = a.Title,
        Description = a.Description,
        EventType = "assignment",
        StartsAtUtc = a.DueDate,
        IsSynthesized = true,
    };

    /// <summary>Get the calendar for a course (events + synthesized assignment due dates).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CourseEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CourseEventDto>>> GetCalendar(
        [FromQuery] int courseId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var (events, unlinkedAssignments) = await _courseEventService.GetCourseCalendarAsync(
                courseId, from, to, _currentUserService.GetCurrentUser());

            var dtos = events.Select(MapToDto)
                .Concat(unlinkedAssignments.Select(a => MapAssignmentToDto(a, courseId)))
                .OrderBy(d => d.StartsAtUtc)
                .ToList();

            return Ok(dtos);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar for course {CourseId}", courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Create a calendar event.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseEventDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CourseEventDto>> CreateEvent([FromBody] CourseEventCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var courseEvent = new CourseEvent
            {
                CourseId = request.CourseId,
                ModuleId = request.ModuleId,
                AssignmentId = request.AssignmentId,
                Title = request.Title,
                Description = request.Description,
                EventType = request.EventType,
                StartsAtUtc = DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc),
                EndsAtUtc = request.EndsAtUtc.HasValue
                    ? DateTime.SpecifyKind(request.EndsAtUtc.Value, DateTimeKind.Utc)
                    : null,
                Remind7Days = request.Remind7Days,
                Remind3Days = request.Remind3Days,
                Remind2Days = request.Remind2Days,
                Remind24Hours = request.Remind24Hours,
            };

            var created = await _courseEventService.CreateAsync(courseEvent, _currentUserService.GetCurrentUser());
            return StatusCode(201, MapToDto(created));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating course event");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Update a calendar event.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CourseEventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CourseEventDto>> UpdateEvent(int id, [FromBody] CourseEventUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _courseEventService.UpdateAsync(id, new CourseEvent
            {
                CourseId = 0, // ignored — the existing event's course is authoritative
                Title = request.Title,
                Description = request.Description,
                EventType = request.EventType,
                StartsAtUtc = DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc),
                EndsAtUtc = request.EndsAtUtc.HasValue
                    ? DateTime.SpecifyKind(request.EndsAtUtc.Value, DateTimeKind.Utc)
                    : null,
                ModuleId = request.ModuleId,
                Remind7Days = request.Remind7Days,
                Remind3Days = request.Remind3Days,
                Remind2Days = request.Remind2Days,
                Remind24Hours = request.Remind24Hours,
            }, _currentUserService.GetCurrentUser());

            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course event {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Delete a calendar event.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteEvent(int id)
    {
        try
        {
            await _courseEventService.DeleteAsync(id, _currentUserService.GetCurrentUser());
            return Ok(new { message = "Event deleted" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course event {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
