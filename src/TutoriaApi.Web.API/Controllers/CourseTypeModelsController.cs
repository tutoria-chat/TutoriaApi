using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages course type to AI model mappings.
/// </summary>
/// <remarks>
/// Course type models define which AI models are recommended for different types of courses
/// (e.g., MathLogic, Programming, TheoryText). Universities can override these defaults
/// with their own preferred models.
///
/// **Authorization**: SuperAdmin only for all operations.
/// </remarks>
[ApiController]
[Route("api/course-type-models")]
[Authorize(Policy = "SuperAdminOnly")]
public class CourseTypeModelsController : ControllerBase
{
    private readonly ICourseTypeModelService _courseTypeModelService;
    private readonly ILogger<CourseTypeModelsController> _logger;

    public CourseTypeModelsController(
        ICourseTypeModelService courseTypeModelService,
        ILogger<CourseTypeModelsController> logger)
    {
        _courseTypeModelService = courseTypeModelService;
        _logger = logger;
    }

    /// <summary>
    /// Get course type model mappings, optionally filtered by course type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CourseTypeModelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CourseTypeModelDto>>> GetCourseTypeModels([FromQuery] string? courseType = null)
    {
        try
        {
            IEnumerable<CourseTypeModel> models;

            if (!string.IsNullOrWhiteSpace(courseType))
            {
                models = await _courseTypeModelService.GetByCourseTypeAsync(courseType);
            }
            else
            {
                models = await _courseTypeModelService.GetActiveAsync();
            }

            var dtos = models.Select(m => new CourseTypeModelDto
            {
                Id = m.Id,
                CourseType = m.CourseType,
                AIModelId = m.AIModelId,
                AIModelName = m.AIModel?.ModelName,
                AIModelDisplayName = m.AIModel?.DisplayName,
                Priority = m.Priority,
                IsActive = m.IsActive,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting course type models");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new course type to AI model mapping.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseTypeModelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseTypeModelDto>> CreateCourseTypeModel([FromBody] CourseTypeModelCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var courseTypeModel = new CourseTypeModel
            {
                CourseType = request.CourseType,
                AIModelId = request.AIModelId,
                Priority = request.Priority,
                IsActive = request.IsActive
            };

            var created = await _courseTypeModelService.CreateAsync(courseTypeModel);

            _logger.LogInformation("Created course type model mapping for '{CourseType}' with ID {Id}",
                created.CourseType, created.Id);

            // Reload with navigation properties
            var full = await _courseTypeModelService.GetByIdAsync(created.Id);

            var dto = new CourseTypeModelDto
            {
                Id = created.Id,
                CourseType = created.CourseType,
                AIModelId = created.AIModelId,
                AIModelName = full?.AIModel?.ModelName,
                AIModelDisplayName = full?.AIModel?.DisplayName,
                Priority = created.Priority,
                IsActive = created.IsActive,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetCourseTypeModels), null, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating course type model mapping");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing course type model mapping.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CourseTypeModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseTypeModelDto>> UpdateCourseTypeModel(int id, [FromBody] CourseTypeModelUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var courseTypeModel = new CourseTypeModel
            {
                CourseType = request.CourseType,
                AIModelId = request.AIModelId,
                Priority = request.Priority,
                IsActive = request.IsActive
            };

            var updated = await _courseTypeModelService.UpdateAsync(id, courseTypeModel);

            // Reload with navigation properties
            var full = await _courseTypeModelService.GetByIdAsync(updated.Id);

            var dto = new CourseTypeModelDto
            {
                Id = updated.Id,
                CourseType = updated.CourseType,
                AIModelId = updated.AIModelId,
                AIModelName = full?.AIModel?.ModelName,
                AIModelDisplayName = full?.AIModel?.DisplayName,
                Priority = updated.Priority,
                IsActive = updated.IsActive,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };

            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Course type model mapping not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course type model mapping with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a course type model mapping.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteCourseTypeModel(int id)
    {
        try
        {
            await _courseTypeModelService.DeleteAsync(id);

            _logger.LogInformation("Deleted course type model mapping with ID {Id}", id);

            return Ok(new { message = "Course type model mapping deleted successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Course type model mapping not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course type model mapping with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get university-specific course type model overrides.
    /// </summary>
    [HttpGet("university/{universityId}")]
    [ProducesResponseType(typeof(List<UniversityCourseTypeModelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UniversityCourseTypeModelDto>>> GetUniversityOverrides(int universityId)
    {
        try
        {
            var overrides = await _courseTypeModelService.GetUniversityOverridesAsync(universityId);

            var dtos = overrides.Select(o => new UniversityCourseTypeModelDto
            {
                Id = o.Id,
                UniversityId = o.UniversityId,
                UniversityName = o.University?.Name,
                CourseType = o.CourseType,
                AIModelId = o.AIModelId,
                AIModelName = o.AIModel?.ModelName,
                AIModelDisplayName = o.AIModel?.DisplayName,
                Priority = o.Priority,
                IsActive = o.IsActive,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting university overrides for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a university-specific course type model override.
    /// </summary>
    [HttpPost("university/{universityId}")]
    [ProducesResponseType(typeof(UniversityCourseTypeModelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UniversityCourseTypeModelDto>> CreateUniversityOverride(
        int universityId,
        [FromBody] UniversityCourseTypeModelCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var override_ = new UniversityCourseTypeModel
            {
                UniversityId = universityId,
                CourseType = request.CourseType,
                AIModelId = request.AIModelId,
                Priority = request.Priority,
                IsActive = request.IsActive
            };

            var created = await _courseTypeModelService.CreateUniversityOverrideAsync(override_);

            _logger.LogInformation(
                "Created university override for university {UniversityId}, course type '{CourseType}' with ID {Id}",
                universityId, created.CourseType, created.Id);

            var dto = new UniversityCourseTypeModelDto
            {
                Id = created.Id,
                UniversityId = created.UniversityId,
                CourseType = created.CourseType,
                AIModelId = created.AIModelId,
                Priority = created.Priority,
                IsActive = created.IsActive,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetUniversityOverrides), new { universityId }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating university override for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
