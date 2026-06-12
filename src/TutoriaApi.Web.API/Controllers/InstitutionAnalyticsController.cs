using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Institution / class / discipline statistics and pedagogical alerts for staff
/// dashboards. Same route prefix and access policy as the main analytics
/// controller; kept separate so the gamification-ledger rollups don't bloat it.
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "AnalyticsAccess")]
public class InstitutionAnalyticsController : ControllerBase
{
    private readonly IInstitutionStatsService _statsService;
    private readonly ILogger<InstitutionAnalyticsController> _logger;

    public InstitutionAnalyticsController(
        IInstitutionStatsService statsService,
        ILogger<InstitutionAnalyticsController> logger)
    {
        _statsService = statsService;
        _logger = logger;
    }

    /// <summary>Per-course engagement stats (class/course level).</summary>
    [HttpGet("course-stats")]
    [ProducesResponseType(typeof(CourseStatsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CourseStatsResponseDto>> GetCourseStats(
        [FromQuery] int days = 30,
        [FromQuery] int? universityId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();
            var result = await _statsService.GetCourseStatsAsync(userId, userRole, userUniversityId, universityId, days);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting course stats");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Per-module (discipline) breakdown for one course.</summary>
    [HttpGet("course-stats/{courseId:int}/modules")]
    [ProducesResponseType(typeof(ModuleStatsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleStatsResponseDto>> GetCourseModuleStats(
        int courseId,
        [FromQuery] int days = 30)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();
            var result = await _statsService.GetCourseModuleStatsAsync(userId, userRole, userUniversityId, courseId, days);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module stats for course {CourseId}", courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Prioritized pedagogical alerts: course evasion + failing quiz concepts.</summary>
    [HttpGet("pedagogical-alerts")]
    [ProducesResponseType(typeof(PedagogicalAlertsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PedagogicalAlertsResponseDto>> GetPedagogicalAlerts(
        [FromQuery] int days = 14,
        [FromQuery] int? universityId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();
            var result = await _statsService.GetPedagogicalAlertsAsync(userId, userRole, userUniversityId, universityId, days);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pedagogical alerts");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private (int userId, string userRole, int? userUniversityId) GetUserContext()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        var universityIdClaim = User.FindFirst("university_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID in token");
        if (string.IsNullOrEmpty(userRoleClaim))
            throw new UnauthorizedAccessException("Invalid user role in token");

        int? universityId = null;
        if (!string.IsNullOrEmpty(universityIdClaim) && int.TryParse(universityIdClaim, out var univId))
            universityId = univId;

        return (userId, userRoleClaim, universityId);
    }
}
