using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Enums;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class AIModelService : IAIModelService
{
    private readonly IAIModelRepository _aiModelRepository;
    private readonly IUniversityRepository _universityRepository;

    public AIModelService(
        IAIModelRepository aiModelRepository,
        IUniversityRepository universityRepository)
    {
        _aiModelRepository = aiModelRepository;
        _universityRepository = universityRepository;
    }

    public async Task<List<AIModelListViewModel>> GetAIModelsAsync(
        string? provider = null,
        bool? isActive = null,
        bool includeDeprecated = false,
        int? universityId = null)
    {
        int? maxTier = null;

        // If filtering by university, get the university's subscription tier
        if (universityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university != null)
            {
                maxTier = university.SubscriptionTier;
            }
        }

        // Get filtered AI models
        var aiModels = await _aiModelRepository.SearchAsync(provider, isActive, includeDeprecated, maxTier);

        // Build view models with module counts
        var viewModels = new List<AIModelListViewModel>();
        foreach (var aiModel in aiModels)
        {
            var modulesCount = await _aiModelRepository.GetModulesCountAsync(aiModel.Id);
            viewModels.Add(new AIModelListViewModel
            {
                AIModel = aiModel,
                ModulesCount = modulesCount
            });
        }

        return viewModels;
    }

    public async Task<AIModelDetailViewModel?> GetAIModelWithDetailsAsync(int id)
    {
        var aiModel = await _aiModelRepository.GetByIdAsync(id);

        if (aiModel == null)
        {
            return null;
        }

        var modulesCount = await _aiModelRepository.GetModulesCountAsync(id);

        return new AIModelDetailViewModel
        {
            AIModel = aiModel,
            ModulesCount = modulesCount
        };
    }

    public async Task<AIModel> CreateAsync(AIModel aiModel)
    {
        // Validate: Check if model with same name already exists
        var exists = await _aiModelRepository.ExistsByModelNameAsync(aiModel.ModelName);
        if (exists)
        {
            throw new InvalidOperationException("AI model with this name already exists");
        }

        // Normalize provider to lowercase
        aiModel.Provider = aiModel.Provider.ToLower();

        return await _aiModelRepository.AddAsync(aiModel);
    }

    public async Task<AIModel> UpdateAsync(int id, AIModel aiModel)
    {
        var existing = await _aiModelRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("AI model not found");
        }

        // Update properties (note: ModelName and Provider should NOT be changed)
        existing.DisplayName = aiModel.DisplayName;
        existing.MaxTokens = aiModel.MaxTokens;
        existing.SupportsVision = aiModel.SupportsVision;
        existing.SupportsFunctionCalling = aiModel.SupportsFunctionCalling;
        existing.InputCostPer1M = aiModel.InputCostPer1M;
        existing.OutputCostPer1M = aiModel.OutputCostPer1M;
        existing.RequiredTier = aiModel.RequiredTier;
        existing.IsActive = aiModel.IsActive;
        existing.IsDeprecated = aiModel.IsDeprecated;
        existing.DeprecationDate = aiModel.DeprecationDate;
        existing.Description = aiModel.Description;
        existing.RecommendedFor = aiModel.RecommendedFor;

        // Auto-set deprecation date if marking as deprecated and no date exists
        if (existing.IsDeprecated && existing.DeprecationDate == null)
        {
            existing.DeprecationDate = DateTime.UtcNow;
        }

        await _aiModelRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task<(bool Success, int AffectedModules)> SoftDeleteAsync(int id)
    {
        var aiModel = await _aiModelRepository.GetByIdAsync(id);
        if (aiModel == null)
        {
            throw new KeyNotFoundException("AI model not found");
        }

        // Get count of modules using this model
        var modulesCount = await _aiModelRepository.GetModulesCountAsync(id);

        // Soft delete - mark as inactive
        aiModel.IsActive = false;
        await _aiModelRepository.UpdateAsync(aiModel);

        return (true, modulesCount);
    }

    public async Task<AIModel?> SelectModelByCourseTypeAsync(CourseType courseType, int universityTier)
    {
        // Course type to model preferences mapping
        // This mirrors the logic from frontend: tutoria-ui/lib/course-type-utils.ts
        var modelPreferences = new Dictionary<CourseType, Dictionary<string, string>>
        {
            [CourseType.MathLogic] = new Dictionary<string, string>
            {
                ["basic"] = "gpt-3.5-turbo",
                ["standard"] = "gpt-4",
                ["premium"] = "gpt-4o"
            },
            [CourseType.Programming] = new Dictionary<string, string>
            {
                ["basic"] = "claude-3-haiku-20240307",
                ["standard"] = "claude-3-7-sonnet-20250219",
                ["premium"] = "claude-sonnet-4-5"
            },
            [CourseType.TheoryText] = new Dictionary<string, string>
            {
                ["basic"] = "claude-3-haiku-20240307",
                ["standard"] = "claude-3-5-haiku-20241022",
                ["premium"] = "claude-haiku-4-5"
            }
        };

        // Map subscription tier number to tier string
        string tierString;
        if (universityTier >= 3)
        {
            tierString = "premium";
        }
        else if (universityTier == 2)
        {
            tierString = "standard";
        }
        else
        {
            tierString = "basic";
        }

        // Get the preferred model name for this course type and tier
        var modelName = modelPreferences[courseType][tierString];

        // Find the model in the database
        var model = await _aiModelRepository.GetByModelNameAsync(modelName);

        // If the preferred model isn't found, try fallback models for lower tiers
        if (model == null)
        {
            if (tierString == "premium")
            {
                // Try standard, then basic
                var standardModelName = modelPreferences[courseType]["standard"];
                model = await _aiModelRepository.GetByModelNameAsync(standardModelName);

                if (model == null)
                {
                    var basicModelName = modelPreferences[courseType]["basic"];
                    model = await _aiModelRepository.GetByModelNameAsync(basicModelName);
                }
            }
            else if (tierString == "standard")
            {
                // Try basic
                var basicModelName = modelPreferences[courseType]["basic"];
                model = await _aiModelRepository.GetByModelNameAsync(basicModelName);
            }
        }

        return model;
    }
}
