using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
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

    private static PlanDto MapToDto(Plan p) => new()
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
        HasAssignments = p.HasAssignments,
        TrialDays = p.TrialDays,
        DisplayOrder = p.DisplayOrder,
        IsActive = p.IsActive,
        IsCustom = p.IsCustom,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

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
            return Ok(plans.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plans");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get all plans including inactive (SuperAdmin only).
    /// </summary>
    [HttpGet("all")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(List<PlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanDto>>> GetAllPlans()
    {
        try
        {
            var plans = await _planService.GetAllAsync();
            return Ok(plans.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all plans");
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

            return Ok(MapToDto(plan));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plan with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new plan (SuperAdmin only).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanDto>> CreatePlan([FromBody] PlanCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var plan = new Plan
            {
                Name = request.Name,
                Slug = request.Slug,
                Description = request.Description,
                MonthlyPriceBRL = request.MonthlyPriceBRL,
                StripePriceId = request.StripePriceId,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
                HasAIQuizzes = request.HasAIQuizzes,
                HasWhatsApp = request.HasWhatsApp,
                HasPrioritySupport = request.HasPrioritySupport,
                HasCustomModelConfig = request.HasCustomModelConfig,
                HasAssignments = request.HasAssignments,
                TrialDays = request.TrialDays,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                IsCustom = request.IsCustom
            };

            var created = await _planService.CreateAsync(plan);
            _logger.LogInformation("Created plan '{Name}' with ID {Id}", created.Name, created.Id);

            return CreatedAtAction(nameof(GetPlan), new { id = created.Id }, MapToDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing plan (SuperAdmin only).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanDto>> UpdatePlan(int id, [FromBody] PlanUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var plan = new Plan
            {
                Name = request.Name,
                Slug = request.Slug,
                Description = request.Description,
                MonthlyPriceBRL = request.MonthlyPriceBRL,
                StripePriceId = request.StripePriceId,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
                HasAIQuizzes = request.HasAIQuizzes,
                HasWhatsApp = request.HasWhatsApp,
                HasPrioritySupport = request.HasPrioritySupport,
                HasCustomModelConfig = request.HasCustomModelConfig,
                HasAssignments = request.HasAssignments,
                TrialDays = request.TrialDays,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                IsCustom = request.IsCustom
            };

            var updated = await _planService.UpdateAsync(id, plan);
            _logger.LogInformation("Updated plan '{Name}' with ID {Id}", updated.Name, updated.Id);

            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Plan not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plan with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Soft-delete a plan by setting IsActive to false (SuperAdmin only).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePlan(int id)
    {
        try
        {
            var existing = await _planService.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Plan not found" });
            }

            // Soft delete: set IsActive = false instead of removing from DB
            existing.IsActive = false;
            await _planService.UpdateAsync(id, existing);
            _logger.LogInformation("Soft-deleted plan with ID {Id}", id);

            return Ok(new { message = "Plan deactivated successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Plan not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting plan with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
