using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Course lifecycle on the course page: the duration timer, and the actions to
/// pause it, hold it for 7 more days, mark the course "not in use", reactivate
/// it, or remove it from the list (soft delete, only once inactive).
/// </summary>
/// <remarks>
/// Actions are allowed for the professors assigned to the course and for any
/// role above professor in the course's university (super admins everywhere).
/// </remarks>
[ApiController]
[Route("api/courses/{courseId:int}/lifecycle")]
[Authorize]
public class CourseLifecycleController : ControllerBase
{
    private readonly ICourseLifecycleService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CourseLifecycleController> _logger;

    public CourseLifecycleController(
        ICourseLifecycleService service,
        ICurrentUserService currentUserService,
        ILogger<CourseLifecycleController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>Where the course is in its lifecycle, and whether the caller may act on it.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CourseLifecycleDto), StatusCodes.Status200OK)]
    public Task<ActionResult<CourseLifecycleDto>> Get(int courseId)
        => Run(courseId, "get", () => _service.GetAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>Pause auto-removal until someone resumes it.</summary>
    [HttpPost("pause")]
    public Task<ActionResult<CourseLifecycleDto>> Pause(int courseId)
        => Run(courseId, "pause", () => _service.PauseAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>Resume auto-removal (always at least 7 days away, with a fresh warning).</summary>
    [HttpPost("resume")]
    public Task<ActionResult<CourseLifecycleDto>> Resume(int courseId)
        => Run(courseId, "resume", () => _service.ResumeAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>Hold removal for 7 more days.</summary>
    [HttpPost("extend")]
    public Task<ActionResult<CourseLifecycleDto>> Extend(int courseId)
        => Run(courseId, "extend", () => _service.ExtendAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>"Course not in use": stop alerts, auto-removal and widget access. Reversible.</summary>
    [HttpPost("deactivate")]
    public Task<ActionResult<CourseLifecycleDto>> Deactivate(int courseId)
        => Run(courseId, "deactivate", () => _service.DeactivateAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>Bring an inactive course back.</summary>
    [HttpPost("reactivate")]
    public Task<ActionResult<CourseLifecycleDto>> Reactivate(int courseId)
        => Run(courseId, "reactivate", () => _service.ReactivateAsync(courseId, _currentUserService.GetCurrentUser()));

    /// <summary>Remove an inactive course from the list (soft delete).</summary>
    [HttpPost("delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(int courseId)
    {
        try
        {
            await _service.DeleteAsync(courseId, _currentUserService.GetCurrentUser());
            return NoContent();
        }
        catch (Exception ex)
        {
            return Fail(ex, courseId, "delete");
        }
    }

    private async Task<ActionResult<CourseLifecycleDto>> Run(int courseId, string action, Func<Task<CourseLifecycleInfo>> call)
    {
        try
        {
            return Ok(CourseLifecycleDto.From(await call()));
        }
        catch (Exception ex)
        {
            return Fail(ex, courseId, action);
        }
    }

    private ActionResult Fail(Exception ex, int courseId, string action)
    {
        switch (ex)
        {
            case KeyNotFoundException:
                return NotFound(new { message = "Course not found" });
            case UnauthorizedAccessException:
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            case InvalidOperationException:
                return BadRequest(new { message = ex.Message });
            default:
                _logger.LogError(ex, "Course lifecycle '{Action}' failed for course {CourseId}", action, courseId);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
