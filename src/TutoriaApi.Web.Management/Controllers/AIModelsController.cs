using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages AI models available for use by tutoring modules.
/// </summary>
/// <remarks>
/// AI models represent the different LLM providers and models (OpenAI GPT-4, Claude, etc.)
/// that can be assigned to tutoring modules. Each module can use a specific AI model for
/// generating responses to students.
///
/// **Authorization**:
/// - Read operations (GET): Authenticated users
/// - Write operations (POST, PUT, DELETE): SuperAdmin only
///
/// **Related Entities**:
/// - Modules (children) - modules that use this AI model
/// </remarks>
[ApiController]
[Route("api/ai-models")]
[Authorize]
public class AIModelsController : ControllerBase
{
    private readonly IAIModelService _aiModelService;
    private readonly ILogger<AIModelsController> _logger;

    public AIModelsController(
        IAIModelService aiModelService,
        ILogger<AIModelsController> logger)
    {
        _aiModelService = aiModelService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of AI models with optional filtering.
    /// </summary>
    /// <param name="provider">Filter by provider (openai, anthropic)</param>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="includeDeprecated">Include deprecated models (default: false)</param>
    /// <param name="universityId">Filter by university subscription tier</param>
    /// <returns>List of AI models.</returns>
    /// <remarks>
    /// Returns all AI models with filtering options. Useful for populating dropdowns
    /// or displaying available models to administrators.
    ///
    /// **Filtering**:
    /// - provider: Filter by 'openai' or 'anthropic'
    /// - isActive: Filter by active/inactive status
    /// - includeDeprecated: Include deprecated models (default: false)
    /// - universityId: Filter models by university's subscription tier (e.g., Tier 3 unis get all models)
    ///
    /// **Performance**: Single query with module count projection.
    /// </remarks>
    /// <response code="200">Returns list of AI models.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<AIModelListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AIModelListDto>>> GetAIModels(
        [FromQuery] string? provider = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool includeDeprecated = false,
        [FromQuery] int? universityId = null)
    {
        try
        {
            var viewModels = await _aiModelService.GetAIModelsAsync(provider, isActive, includeDeprecated, universityId);

            var dtos = viewModels.Select(vm => new AIModelListDto
            {
                Id = vm.AIModel.Id,
                ModelName = vm.AIModel.ModelName,
                DisplayName = vm.AIModel.DisplayName,
                Provider = vm.AIModel.Provider,
                MaxTokens = vm.AIModel.MaxTokens,
                SupportsVision = vm.AIModel.SupportsVision,
                SupportsFunctionCalling = vm.AIModel.SupportsFunctionCalling,
                InputCostPer1M = vm.AIModel.InputCostPer1M,
                OutputCostPer1M = vm.AIModel.OutputCostPer1M,
                RequiredTier = vm.AIModel.RequiredTier,
                IsActive = vm.AIModel.IsActive,
                IsDeprecated = vm.AIModel.IsDeprecated,
                RecommendedFor = vm.AIModel.RecommendedFor,
                ModulesCount = vm.ModulesCount
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI models");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get detailed information about a specific AI model.
    /// </summary>
    /// <param name="id">AI model ID</param>
    /// <returns>Detailed AI model information including modules count.</returns>
    /// <response code="200">Returns AI model details.</response>
    /// <response code="404">AI model not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AIModelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AIModelDetailDto>> GetAIModel(int id)
    {
        try
        {
            var viewModel = await _aiModelService.GetAIModelWithDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "AI model not found" });
            }

            var dto = new AIModelDetailDto
            {
                Id = viewModel.AIModel.Id,
                ModelName = viewModel.AIModel.ModelName,
                DisplayName = viewModel.AIModel.DisplayName,
                Provider = viewModel.AIModel.Provider,
                MaxTokens = viewModel.AIModel.MaxTokens,
                SupportsVision = viewModel.AIModel.SupportsVision,
                SupportsFunctionCalling = viewModel.AIModel.SupportsFunctionCalling,
                InputCostPer1M = viewModel.AIModel.InputCostPer1M,
                OutputCostPer1M = viewModel.AIModel.OutputCostPer1M,
                RequiredTier = viewModel.AIModel.RequiredTier,
                IsActive = viewModel.AIModel.IsActive,
                IsDeprecated = viewModel.AIModel.IsDeprecated,
                DeprecationDate = viewModel.AIModel.DeprecationDate,
                Description = viewModel.AIModel.Description,
                RecommendedFor = viewModel.AIModel.RecommendedFor,
                ModulesCount = viewModel.ModulesCount,
                CreatedAt = viewModel.AIModel.CreatedAt,
                UpdatedAt = viewModel.AIModel.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI model with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new AI model.
    /// </summary>
    /// <param name="request">AI model creation request</param>
    /// <returns>Created AI model details.</returns>
    /// <remarks>
    /// **SuperAdmin only**: Only super administrators can create new AI models.
    ///
    /// **Validation**:
    /// - ModelName must be unique
    /// - Provider must be 'openai' or 'anthropic'
    /// - MaxTokens must be greater than 0
    /// - Costs (if provided) must be non-negative
    /// </remarks>
    /// <response code="201">AI model created successfully.</response>
    /// <response code="400">Validation failed or model already exists.</response>
    /// <response code="403">User is not a super administrator.</response>
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(AIModelDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AIModelDetailDto>> CreateAIModel([FromBody] AIModelCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var aiModel = new AIModel
            {
                ModelName = request.ModelName,
                DisplayName = request.DisplayName,
                Provider = request.Provider,
                MaxTokens = request.MaxTokens,
                SupportsVision = request.SupportsVision,
                SupportsFunctionCalling = request.SupportsFunctionCalling,
                InputCostPer1M = request.InputCostPer1M,
                OutputCostPer1M = request.OutputCostPer1M,
                RequiredTier = request.RequiredTier,
                IsActive = request.IsActive,
                IsDeprecated = request.IsDeprecated,
                DeprecationDate = request.DeprecationDate,
                Description = request.Description,
                RecommendedFor = request.RecommendedFor
            };

            var created = await _aiModelService.CreateAsync(aiModel);

            _logger.LogInformation("Created AI model {ModelName} with ID {Id}", created.ModelName, created.Id);

            var dto = new AIModelDetailDto
            {
                Id = created.Id,
                ModelName = created.ModelName,
                DisplayName = created.DisplayName,
                Provider = created.Provider,
                MaxTokens = created.MaxTokens,
                SupportsVision = created.SupportsVision,
                SupportsFunctionCalling = created.SupportsFunctionCalling,
                InputCostPer1M = created.InputCostPer1M,
                OutputCostPer1M = created.OutputCostPer1M,
                RequiredTier = created.RequiredTier,
                IsActive = created.IsActive,
                IsDeprecated = created.IsDeprecated,
                DeprecationDate = created.DeprecationDate,
                Description = created.Description,
                RecommendedFor = created.RecommendedFor,
                ModulesCount = 0,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetAIModel), new { id = created.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating AI model");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing AI model.
    /// </summary>
    /// <param name="id">AI model ID</param>
    /// <param name="request">AI model update request</param>
    /// <returns>Updated AI model details.</returns>
    /// <remarks>
    /// **SuperAdmin only**: Only super administrators can update AI models.
    ///
    /// **Note**: ModelName and Provider cannot be changed after creation.
    /// Only metadata and capabilities can be updated.
    ///
    /// **Common Use Cases**:
    /// - Update pricing when provider changes rates
    /// - Mark model as deprecated when provider sunsets it
    /// - Update capabilities when provider adds features
    /// - Activate/deactivate models
    /// </remarks>
    /// <response code="200">AI model updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">User is not a super administrator.</response>
    /// <response code="404">AI model not found.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(AIModelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AIModelDetailDto>> UpdateAIModel(int id, [FromBody] AIModelUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Get existing model first
            var existing = await _aiModelService.GetAIModelWithDetailsAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "AI model not found" });
            }

            // Build update entity from existing + request
            var aiModel = existing.AIModel;
            aiModel.DisplayName = request.DisplayName ?? aiModel.DisplayName;
            aiModel.MaxTokens = request.MaxTokens ?? aiModel.MaxTokens;
            aiModel.SupportsVision = request.SupportsVision ?? aiModel.SupportsVision;
            aiModel.SupportsFunctionCalling = request.SupportsFunctionCalling ?? aiModel.SupportsFunctionCalling;
            aiModel.InputCostPer1M = request.InputCostPer1M ?? aiModel.InputCostPer1M;
            aiModel.OutputCostPer1M = request.OutputCostPer1M ?? aiModel.OutputCostPer1M;
            aiModel.RequiredTier = request.RequiredTier ?? aiModel.RequiredTier;
            aiModel.IsActive = request.IsActive ?? aiModel.IsActive;
            aiModel.IsDeprecated = request.IsDeprecated ?? aiModel.IsDeprecated;
            aiModel.DeprecationDate = request.DeprecationDate ?? aiModel.DeprecationDate;
            aiModel.Description = request.Description ?? aiModel.Description;
            aiModel.RecommendedFor = request.RecommendedFor ?? aiModel.RecommendedFor;

            var updated = await _aiModelService.UpdateAsync(id, aiModel);

            _logger.LogInformation("Updated AI model {ModelName} with ID {Id}", updated.ModelName, updated.Id);

            // Get full details for response
            var viewModel = await _aiModelService.GetAIModelWithDetailsAsync(id);

            var dto = new AIModelDetailDto
            {
                Id = viewModel!.AIModel.Id,
                ModelName = viewModel.AIModel.ModelName,
                DisplayName = viewModel.AIModel.DisplayName,
                Provider = viewModel.AIModel.Provider,
                MaxTokens = viewModel.AIModel.MaxTokens,
                SupportsVision = viewModel.AIModel.SupportsVision,
                SupportsFunctionCalling = viewModel.AIModel.SupportsFunctionCalling,
                InputCostPer1M = viewModel.AIModel.InputCostPer1M,
                OutputCostPer1M = viewModel.AIModel.OutputCostPer1M,
                RequiredTier = viewModel.AIModel.RequiredTier,
                IsActive = viewModel.AIModel.IsActive,
                IsDeprecated = viewModel.AIModel.IsDeprecated,
                DeprecationDate = viewModel.AIModel.DeprecationDate,
                Description = viewModel.AIModel.Description,
                RecommendedFor = viewModel.AIModel.RecommendedFor,
                ModulesCount = viewModel.ModulesCount,
                CreatedAt = viewModel.AIModel.CreatedAt,
                UpdatedAt = viewModel.AIModel.UpdatedAt
            };

            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "AI model not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AI model with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete an AI model (soft delete by marking as inactive).
    /// </summary>
    /// <param name="id">AI model ID</param>
    /// <returns>Success message.</returns>
    /// <remarks>
    /// **SuperAdmin only**: Only super administrators can delete AI models.
    ///
    /// **Soft Delete**: This operation marks the model as inactive rather than
    /// permanently deleting it. This preserves referential integrity with modules
    /// that may still reference this model.
    ///
    /// **Warning**: If modules are currently using this model, they will continue
    /// to work but administrators won't be able to assign this model to new modules.
    ///
    /// Consider marking the model as deprecated instead if you want to phase it out gradually.
    /// </remarks>
    /// <response code="200">AI model deleted (deactivated) successfully.</response>
    /// <response code="403">User is not a super administrator.</response>
    /// <response code="404">AI model not found.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAIModel(int id)
    {
        try
        {
            var (success, modulesCount) = await _aiModelService.SoftDeleteAsync(id);

            _logger.LogInformation(
                "Soft deleted AI model with ID {Id}. {ModulesCount} modules were using this model.",
                id,
                modulesCount);

            return Ok(new
            {
                message = "AI model deactivated successfully",
                affectedModules = modulesCount,
                note = "Model marked as inactive. Existing modules can still use it but it won't appear in new module selections."
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "AI model not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting AI model with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
