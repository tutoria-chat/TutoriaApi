using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages subscription plans available on the platform.
/// </summary>
/// <remarks>
/// Plans define the features and limits available to universities.
/// Public read access allows prospective customers to view available plans.
///
/// **Authorization**:
/// - Read operations (GET): Public (no authentication required)
/// - Write operations (POST, PUT, DELETE): SuperAdmin only
/// </remarks>
[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;
    private readonly ILogger<PlansController> _logger;

    public PlansController(
        IPlanService planService,
        ILogger<PlansController> logger)
    {
        _planService = planService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active plans (public endpoint).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<PlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanDto>>> GetPlans()
    {
        try
        {
            var plans = await _planService.GetActivePlansAsync();

            var dtos = plans.Select(p => new PlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                MonthlyPriceBRL = p.MonthlyPriceBRL,
                StripePriceId = p.StripePriceId,
                MaxCourses = p.MaxCourses,
                MaxModules = p.MaxModules,
                MaxStudents = p.MaxStudents,
                HasAIQuizzes = p.HasAIQuizzes,
                HasWhatsApp = p.HasWhatsApp,
                HasPrioritySupport = p.HasPrioritySupport,
                HasCustomModelConfig = p.HasCustomModelConfig,
                TrialDays = p.TrialDays,
                DisplayOrder = p.DisplayOrder,
                IsActive = p.IsActive,
                IsCustom = p.IsCustom,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plans");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get plan details by ID (public endpoint).
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> GetPlan(int id)
    {
        try
        {
            var plan = await _planService.GetByIdAsync(id);
            if (plan == null)
            {
                return NotFound(new { message = "Plan not found" });
            }

            var dto = new PlanDto
            {
                Id = plan.Id,
                Name = plan.Name,
                Slug = plan.Slug,
                Description = plan.Description,
                MonthlyPriceBRL = plan.MonthlyPriceBRL,
                StripePriceId = plan.StripePriceId,
                MaxCourses = plan.MaxCourses,
                MaxModules = plan.MaxModules,
                MaxStudents = plan.MaxStudents,
                HasAIQuizzes = plan.HasAIQuizzes,
                HasWhatsApp = plan.HasWhatsApp,
                HasPrioritySupport = plan.HasPrioritySupport,
                HasCustomModelConfig = plan.HasCustomModelConfig,
                TrialDays = plan.TrialDays,
                DisplayOrder = plan.DisplayOrder,
                IsActive = plan.IsActive,
                IsCustom = plan.IsCustom,
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plan with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
