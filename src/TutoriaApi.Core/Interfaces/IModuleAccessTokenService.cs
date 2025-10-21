using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

// View model for token list
public class ModuleAccessTokenListViewModel
{
    public ModuleAccessToken Token { get; set; } = null!;
    public string? ModuleName { get; set; }
}

// View model for token details with full navigation
public class ModuleAccessTokenDetailViewModel
{
    public ModuleAccessToken Token { get; set; } = null!;
    public string? ModuleName { get; set; }
    public string? CourseName { get; set; }
    public string? UniversityName { get; set; }
    public string? CreatedByName { get; set; }
}

public interface IModuleAccessTokenService
{
    Task<ModuleAccessTokenDetailViewModel?> GetWithDetailsAsync(int id);
    Task<(List<ModuleAccessTokenListViewModel> Items, int Total)> GetPagedAsync(
        int? moduleId,
        int? universityId,
        bool? isActive,
        int page,
        int pageSize,
        User? currentUser);
    Task<ModuleAccessTokenDetailViewModel> CreateAsync(
        int moduleId,
        string name,
        string? description,
        bool allowChat,
        bool allowFileAccess,
        int? expiresInDays,
        User currentUser);
    Task<ModuleAccessTokenDetailViewModel> UpdateAsync(
        int id,
        string? name,
        string? description,
        bool? isActive,
        bool? allowChat,
        bool? allowFileAccess);
    Task DeleteAsync(int id);
}
