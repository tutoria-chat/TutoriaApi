using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
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
    private readonly IAIModelRepository _aiModelRepository;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<AIModelsController> _logger;

    public AIModelsController(
        IAIModelRepository aiModelRepository,
        TutoriaDbContext context,
        ILogger<AIModelsController> logger)
    {
        _aiModelRepository = aiModelRepository;
        _context = context;
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
        var query = _context.AIModels.AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(a => a.Provider == provider.ToLower());
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        if (!includeDeprecated)
        {
            query = query.Where(a => !a.IsDeprecated);
        }

        // Filter by university subscription tier
        if (universityId.HasValue)
        {
            var university = await _context.Universities.FindAsync(universityId.Value);
            if (university != null)
            {
                // Only show models that require tier <= university's subscription tier
                query = query.Where(a => a.RequiredTier <= university.SubscriptionTier);
            }
        }

        var models = await query
            .OrderBy(a => a.Provider)
            .ThenBy(a => a.DisplayName)
            .Select(a => new AIModelListDto
            {
                Id = a.Id,
                ModelName = a.ModelName,
                DisplayName = a.DisplayName,
                Provider = a.Provider,
                MaxTokens = a.MaxTokens,
                SupportsVision = a.SupportsVision,
                SupportsFunctionCalling = a.SupportsFunctionCalling,
                InputCostPer1M = a.InputCostPer1M,
                OutputCostPer1M = a.OutputCostPer1M,
                RequiredTier = a.RequiredTier,
                IsActive = a.IsActive,
                IsDeprecated = a.IsDeprecated,
                RecommendedFor = a.RecommendedFor
            })
            .ToListAsync();

        return Ok(models);
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
        var dto = await _context.AIModels
            .Where(a => a.Id == id)
            .Select(a => new AIModelDetailDto
            {
                Id = a.Id,
                ModelName = a.ModelName,
                DisplayName = a.DisplayName,
                Provider = a.Provider,
                MaxTokens = a.MaxTokens,
                SupportsVision = a.SupportsVision,
                SupportsFunctionCalling = a.SupportsFunctionCalling,
                InputCostPer1M = a.InputCostPer1M,
                OutputCostPer1M = a.OutputCostPer1M,
                RequiredTier = a.RequiredTier,
                IsActive = a.IsActive,
                IsDeprecated = a.IsDeprecated,
                DeprecationDate = a.DeprecationDate,
                Description = a.Description,
                RecommendedFor = a.RecommendedFor,
                ModulesCount = _context.Modules.Count(m => m.AIModelId == a.Id),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (dto == null)
        {
            return NotFound(new { message = "AI model not found" });
        }

        return Ok(dto);
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

        // Check if model with same name already exists
        var existingModel = await _aiModelRepository.GetByModelNameAsync(request.ModelName);
        if (existingModel != null)
        {
            return BadRequest(new { message = "AI model with this name already exists" });
        }

        var aiModel = new AIModel
        {
            ModelName = request.ModelName,
            DisplayName = request.DisplayName,
            Provider = request.Provider.ToLower(),
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

        var created = await _aiModelRepository.AddAsync(aiModel);

        _logger.LogInformation("Created AI model {ModelName} with ID {Id}", created.ModelName, created.Id);

        return CreatedAtAction(nameof(GetAIModel), new { id = created.Id }, new AIModelDetailDto
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
        });
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

        var aiModel = await _aiModelRepository.GetByIdAsync(id);
        if (aiModel == null)
        {
            return NotFound(new { message = "AI model not found" });
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            aiModel.DisplayName = request.DisplayName;
        }

        if (request.MaxTokens.HasValue)
        {
            aiModel.MaxTokens = request.MaxTokens.Value;
        }

        if (request.SupportsVision.HasValue)
        {
            aiModel.SupportsVision = request.SupportsVision.Value;
        }

        if (request.SupportsFunctionCalling.HasValue)
        {
            aiModel.SupportsFunctionCalling = request.SupportsFunctionCalling.Value;
        }

        if (request.InputCostPer1M.HasValue)
        {
            aiModel.InputCostPer1M = request.InputCostPer1M;
        }

        if (request.OutputCostPer1M.HasValue)
        {
            aiModel.OutputCostPer1M = request.OutputCostPer1M;
        }

        if (request.IsActive.HasValue)
        {
            aiModel.IsActive = request.IsActive.Value;
        }

        if (request.IsDeprecated.HasValue)
        {
            aiModel.IsDeprecated = request.IsDeprecated.Value;

            // Auto-set deprecation date if marking as deprecated and no date exists
            if (aiModel.IsDeprecated && aiModel.DeprecationDate == null)
            {
                aiModel.DeprecationDate = DateTime.UtcNow;
            }
        }

        if (request.DeprecationDate.HasValue)
        {
            aiModel.DeprecationDate = request.DeprecationDate;
        }

        if (request.Description != null)
        {
            aiModel.Description = request.Description;
        }

        if (request.RecommendedFor != null)
        {
            aiModel.RecommendedFor = request.RecommendedFor;
        }

        if (request.RequiredTier.HasValue)
        {
            aiModel.RequiredTier = request.RequiredTier.Value;
        }

        await _aiModelRepository.UpdateAsync(aiModel);

        _logger.LogInformation("Updated AI model {ModelName} with ID {Id}", aiModel.ModelName, aiModel.Id);

        var dto = await _context.AIModels
            .Where(a => a.Id == aiModel.Id)
            .Select(a => new AIModelDetailDto
            {
                Id = a.Id,
                ModelName = a.ModelName,
                DisplayName = a.DisplayName,
                Provider = a.Provider,
                MaxTokens = a.MaxTokens,
                SupportsVision = a.SupportsVision,
                SupportsFunctionCalling = a.SupportsFunctionCalling,
                InputCostPer1M = a.InputCostPer1M,
                OutputCostPer1M = a.OutputCostPer1M,
                RequiredTier = a.RequiredTier,
                IsActive = a.IsActive,
                IsDeprecated = a.IsDeprecated,
                DeprecationDate = a.DeprecationDate,
                Description = a.Description,
                RecommendedFor = a.RecommendedFor,
                ModulesCount = _context.Modules.Count(m => m.AIModelId == a.Id),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .FirstOrDefaultAsync();

        return Ok(dto);
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
        var aiModel = await _aiModelRepository.GetByIdAsync(id);
        if (aiModel == null)
        {
            return NotFound(new { message = "AI model not found" });
        }

        // Get count of modules using this model
        var modulesCount = await _context.Modules.CountAsync(m => m.AIModelId == id);

        // Soft delete - mark as inactive instead of hard delete
        aiModel.IsActive = false;
        await _aiModelRepository.UpdateAsync(aiModel);

        _logger.LogInformation(
            "Soft deleted AI model {ModelName} with ID {Id}. {ModulesCount} modules were using this model.",
            aiModel.ModelName,
            aiModel.Id,
            modulesCount);

        return Ok(new
        {
            message = "AI model deactivated successfully",
            affectedModules = modulesCount,
            note = "Model marked as inactive. Existing modules can still use it but it won't appear in new module selections."
        });
    }
}
