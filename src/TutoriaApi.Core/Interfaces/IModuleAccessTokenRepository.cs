using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IModuleAccessTokenRepository : IRepository<ModuleAccessToken>
{
    Task<ModuleAccessToken?> GetWithDetailsAsync(int id);
    Task<ModuleAccessToken?> GetByTokenAsync(string token);
    Task<(IEnumerable<ModuleAccessToken> Items, int Total)> SearchAsync(
        int? moduleId,
        int? universityId,
        bool? isActive,
        int page,
        int pageSize,
        List<int>? allowedModuleIds = null);
    Task<List<ModuleAccessToken>> GetByModuleIdAsync(int moduleId);
    Task<bool> ExistsByTokenAsync(string token);
}
