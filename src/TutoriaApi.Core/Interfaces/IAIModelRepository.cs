using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IAIModelRepository : IRepository<AIModel>
{
    Task<AIModel?> GetWithModulesAsync(int id);
    Task<IEnumerable<AIModel>> GetActiveModelsAsync();
    Task<IEnumerable<AIModel>> GetByProviderAsync(string provider);
    Task<AIModel?> GetByModelNameAsync(string modelName);
    Task<bool> ExistsByModelNameAsync(string modelName);

    Task<List<AIModel>> SearchAsync(
        string? provider = null,
        bool? isActive = null,
        bool includeDeprecated = false,
        int? maxTier = null);

    Task<int> GetModulesCountAsync(int aiModelId);
}
