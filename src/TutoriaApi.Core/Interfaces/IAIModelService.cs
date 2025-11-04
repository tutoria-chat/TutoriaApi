using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Enums;

namespace TutoriaApi.Core.Interfaces;

// View model for AI model list item
public class AIModelListViewModel
{
    public AIModel AIModel { get; set; } = null!;
    public int ModulesCount { get; set; }
}

// View model for AI model detail with related data
public class AIModelDetailViewModel
{
    public AIModel AIModel { get; set; } = null!;
    public int ModulesCount { get; set; }
}

public interface IAIModelService
{
    Task<List<AIModelListViewModel>> GetAIModelsAsync(
        string? provider = null,
        bool? isActive = null,
        bool includeDeprecated = false,
        int? universityId = null);

    Task<AIModelDetailViewModel?> GetAIModelWithDetailsAsync(int id);

    Task<AIModel> CreateAsync(AIModel aiModel);

    Task<AIModel> UpdateAsync(int id, AIModel aiModel);

    Task<(bool Success, int AffectedModules)> SoftDeleteAsync(int id);

    Task<AIModel?> SelectModelByCourseTypeAsync(CourseType courseType, int universityTier);
}
