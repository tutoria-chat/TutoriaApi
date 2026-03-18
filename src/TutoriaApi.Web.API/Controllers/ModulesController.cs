using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages academic modules (course units) and their configuration.
/// </summary>
[ApiController]
[Route("api/modules")]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly IModuleService _moduleService;
    private readonly ICourseRepository _courseRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IAIModelService _aiModelService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuizGenerationService _quizGenerationService;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(
        IModuleService moduleService,
        ICourseRepository courseRepository,
        IModuleRepository moduleRepository,
        ISubscriptionRepository subscriptionRepository,
        IAIModelService aiModelService,
        ICurrentUserService currentUserService,
        IQuizGenerationService quizGenerationService,
        ILogger<ModulesController> logger)
    {
        _moduleService = moduleService;
        _courseRepository = courseRepository;
        _moduleRepository = moduleRepository;
        _subscriptionRepository = subscriptionRepository;
        _aiModelService = aiModelService;
        _currentUserService = currentUserService;
        _quizGenerationService = quizGenerationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ModuleListDto>>> GetModules(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? courseId = null,
        [FromQuery] int? semester = null,
        [FromQuery] int? year = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            var (viewModels, total) = await _moduleService.GetPagedWithCountsAsync(
                courseId,
                semester,
                year,
                search,
                page,
                size,
                _currentUserService.GetCurrentUser());

            var dtos = viewModels.Select(vm => new ModuleListDto
            {
                Id = vm.Module.Id,
                Name = vm.Module.Name,
                Code = vm.Module.Code,
                Description = vm.Module.Description,
                Semester = vm.Module.Semester,
                Year = vm.Module.Year,
                CourseId = vm.Module.CourseId,
                CourseName = vm.CourseName,
                AIModelId = vm.Module.AIModelId,
                AIModelDisplayName = vm.AIModelDisplayName,
                CourseType = vm.Module.CourseType,
                FilesCount = vm.FilesCount,
                TokensCount = vm.TokensCount,
                CreatedAt = vm.Module.CreatedAt,
                UpdatedAt = vm.Module.UpdatedAt
            }).ToList();

            return Ok(new PaginatedResponse<ModuleListDto>
            {
                Items = dtos,
                Total = total,
                Page = page,
                Size = size,
                Pages = (int)Math.Ceiling(total / (double)size)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving modules");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModuleDetailDto>> GetModule(int id)
    {
        try
        {
            var viewModel = await _moduleService.GetWithDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "Module not found" });
            }

            var module = viewModel.Module;
            var course = viewModel.Course;
            var aiModel = viewModel.AIModel;
            var files = viewModel.Files;

            return Ok(new ModuleDetailDto
            {
                Id = module.Id,
                Name = module.Name,
                Code = module.Code,
                Description = module.Description,
                SystemPrompt = module.SystemPrompt,
                Semester = module.Semester,
                Year = module.Year,
                CourseId = module.CourseId,
                Course = course != null ? new CourseDto
                {
                    Id = course.Id,
                    Name = course.Name,
                    Code = course.Code,
                    Description = course.Description
                } : null,
                CourseName = course?.Name,
                UniversityId = course?.UniversityId,
                AIModelId = module.AIModelId,
                AIModel = aiModel != null ? new AIModelListDto
                {
                    Id = aiModel.Id,
                    ModelName = aiModel.ModelName,
                    DisplayName = aiModel.DisplayName,
                    Provider = aiModel.Provider,
                    MaxTokens = aiModel.MaxTokens,
                    SupportsVision = aiModel.SupportsVision,
                    SupportsFunctionCalling = aiModel.SupportsFunctionCalling,
                    InputCostPer1M = aiModel.InputCostPer1M,
                    OutputCostPer1M = aiModel.OutputCostPer1M,
                    RequiredTier = aiModel.RequiredTier,
                    IsActive = aiModel.IsActive,
                    IsDeprecated = aiModel.IsDeprecated,
                    RecommendedFor = aiModel.RecommendedFor,
                    ModulesCount = 0 // Not needed in this context
                } : null,
                CourseType = module.CourseType,
                OpenAIAssistantId = module.OpenAIAssistantId,
                OpenAIVectorStoreId = module.OpenAIVectorStoreId,
                LastPromptImprovedAt = module.LastPromptImprovedAt,
                PromptImprovementCount = module.PromptImprovementCount,
                TutorLanguage = module.TutorLanguage,
                Files = files.Select(f => new FileListDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    FileType = f.FileType,
                    FileName = f.FileName,
                    BlobPath = f.BlobPath,
                    BlobUrl = f.BlobUrl,
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    ModuleId = f.ModuleId,
                    ModuleName = module.Name,
                    IsActive = f.IsActive,
                    OpenAIFileId = f.OpenAIFileId,
                    AnthropicFileId = f.AnthropicFileId,
                    // Video/Transcription fields
                    SourceType = f.SourceType,
                    SourceUrl = f.SourceUrl,
                    TranscriptionStatus = f.TranscriptionStatus,
                    TranscriptWordCount = f.TranscriptWordCount,
                    VideoDurationSeconds = f.VideoDurationSeconds,
                    TranscriptedAt = f.TranscriptedAt,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt
                }).ToList(),
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module {ModuleId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<ModuleDetailDto>> CreateModule([FromBody] ModuleCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Plan enforcement: check module limit
            var courseForLimits = await _courseRepository.GetWithDetailsAsync(request.CourseId);
            if (courseForLimits?.University != null)
            {
                // Check university-level override first (applies to ALL universities including enterprise)
                int? maxModules = courseForLimits.University.MaxModules;

                // If no university-level override and NOT enterprise, check subscription plan
                if (maxModules == null && !courseForLimits.University.IsEnterprise)
                {
                    var subscription = await _subscriptionRepository.GetActiveByUniversityIdAsync(courseForLimits.UniversityId);
                    if (subscription?.Plan != null)
                    {
                        maxModules = subscription.Plan.MaxModules;
                    }
                }

                if (maxModules.HasValue)
                {
                    var existingModules = await _moduleRepository.GetByUniversityIdAsync(courseForLimits.UniversityId);
                    if (existingModules.Count() >= maxModules.Value)
                    {
                        return StatusCode(403, new { message = $"Module limit reached ({maxModules.Value}). Please upgrade your plan to create more modules." });
                    }
                }
            }

            // AI model selection is handled at runtime by Python AI orchestrator
            // via CourseTypeModels / UniversityCourseTypeModels tables (round-robin).
            // Module-level AIModelId is no longer auto-assigned.

            var module = new Module
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                SystemPrompt = request.SystemPrompt,
                Semester = request.Semester,
                Year = request.Year,
                CourseId = request.CourseId,
                AIModelId = request.AIModelId,
                CourseType = request.CourseType,
                TutorLanguage = request.TutorLanguage,
                PromptImprovementCount = 0
            };

            var created = await _moduleService.CreateAsync(module, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Created module {Name} with ID {Id}", created.Name, created.Id);

            // Trigger quiz generation in background (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Use upsert: true to ensure quizzes are generated even on retry
                    await _quizGenerationService.TriggerQuizGenerationAsync(created.Id, count: 50, upsert: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background quiz generation failed for module {ModuleId}", created.Id);
                }
            });

            return CreatedAtAction(nameof(GetModule), new { id = created.Id }, new ModuleDetailDto
            {
                Id = created.Id,
                Name = created.Name,
                Code = created.Code,
                Description = created.Description,
                SystemPrompt = created.SystemPrompt,
                Semester = created.Semester,
                Year = created.Year,
                CourseId = created.CourseId,
                AIModelId = created.AIModelId,
                CourseType = created.CourseType,
                TutorLanguage = created.TutorLanguage,
                PromptImprovementCount = created.PromptImprovementCount,
                Files = new List<FileListDto>(),
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating module");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<ModuleDetailDto>> UpdateModule(int id, [FromBody] ModuleUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var existing = await _moduleService.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Module not found" });
            }

            // Apply updates from request
            if (!string.IsNullOrWhiteSpace(request.Name))
                existing.Name = request.Name;

            if (!string.IsNullOrWhiteSpace(request.Code))
                existing.Code = request.Code;

            if (request.Description != null)
                existing.Description = request.Description;

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                existing.SystemPrompt = request.SystemPrompt;

            if (request.Semester.HasValue)
                existing.Semester = request.Semester;

            if (request.Year.HasValue)
                existing.Year = request.Year;

            if (!string.IsNullOrWhiteSpace(request.TutorLanguage))
                existing.TutorLanguage = request.TutorLanguage;

            // Update CourseType if provided
            if (request.CourseType.HasValue)
                existing.CourseType = request.CourseType;

            // AI model selection is handled at runtime by Python AI orchestrator.
            // Only set AIModelId if explicitly provided (manual override).
            if (request.AIModelId.HasValue)
            {
                existing.AIModelId = request.AIModelId;
                _logger.LogInformation("Manually set AI model to {ModelId} for module {ModuleId}", request.AIModelId, id);
            }

            var updated = await _moduleService.UpdateAsync(id, existing, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Updated module {Name} with ID {Id}", updated.Name, updated.Id);

            // Trigger quiz regeneration in background (fire and forget) - upsert mode
            _ = Task.Run(async () =>
            {
                try
                {
                    await _quizGenerationService.TriggerQuizGenerationAsync(updated.Id, count: 50, upsert: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background quiz regeneration failed for module {ModuleId}", updated.Id);
                }
            });

            var viewModel = await _moduleService.GetWithDetailsAsync(id);

            return Ok(new ModuleDetailDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Code = updated.Code,
                Description = updated.Description,
                SystemPrompt = updated.SystemPrompt,
                Semester = updated.Semester,
                Year = updated.Year,
                CourseId = updated.CourseId,
                Course = viewModel?.Course != null ? new CourseDto
                {
                    Id = viewModel.Course.Id,
                    Name = viewModel.Course.Name,
                    Code = viewModel.Course.Code,
                    Description = viewModel.Course.Description
                } : null,
                CourseName = viewModel?.Course?.Name,
                UniversityId = viewModel?.Course?.UniversityId,
                AIModelId = updated.AIModelId,
                AIModel = viewModel?.AIModel != null ? new AIModelListDto
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
                    RecommendedFor = viewModel.AIModel.RecommendedFor,
                    ModulesCount = 0
                } : null,
                CourseType = updated.CourseType,
                OpenAIAssistantId = updated.OpenAIAssistantId,
                OpenAIVectorStoreId = updated.OpenAIVectorStoreId,
                LastPromptImprovedAt = updated.LastPromptImprovedAt,
                PromptImprovementCount = updated.PromptImprovementCount,
                TutorLanguage = updated.TutorLanguage,
                Files = viewModel?.Files?.Select(f => new FileListDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    FileType = f.FileType,
                    FileName = f.FileName,
                    BlobPath = f.BlobPath,
                    BlobUrl = f.BlobUrl,
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    ModuleId = f.ModuleId,
                    ModuleName = updated.Name,
                    IsActive = f.IsActive,
                    OpenAIFileId = f.OpenAIFileId,
                    AnthropicFileId = f.AnthropicFileId,
                    // Video/Transcription fields
                    SourceType = f.SourceType,
                    SourceUrl = f.SourceUrl,
                    TranscriptionStatus = f.TranscriptionStatus,
                    TranscriptWordCount = f.TranscriptWordCount,
                    VideoDurationSeconds = f.VideoDurationSeconds,
                    TranscriptedAt = f.TranscriptedAt,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt
                }).ToList() ?? new List<FileListDto>(),
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module {ModuleId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult> DeleteModule(int id)
    {
        try
        {
            await _moduleService.DeleteAsync(id, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Deleted module with ID {Id}", id);

            return Ok(new { message = "Module deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting module {ModuleId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
