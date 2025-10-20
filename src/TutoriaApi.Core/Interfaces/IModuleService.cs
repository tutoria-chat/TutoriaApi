using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

// View model for module list with counts
public class ModuleListViewModel
{
    public Module Module { get; set; } = null!;
    public string? CourseName { get; set; }
    public string? AIModelDisplayName { get; set; }
    public int FilesCount { get; set; }
    public int TokensCount { get; set; }
}

// View model for module details with full navigation
public class ModuleDetailViewModel
{
    public Module Module { get; set; } = null!;
    public Course? Course { get; set; }
    public AIModel? AIModel { get; set; }
    public List<TutoriaApi.Core.Entities.File> Files { get; set; } = new();
}

public interface IModuleService
{
    Task<Module?> GetByIdAsync(int id);
    Task<ModuleDetailViewModel?> GetWithDetailsAsync(int id);
    Task<(IEnumerable<Module> Items, int Total)> GetPagedAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize);
    Task<(List<ModuleListViewModel> Items, int Total)> GetPagedWithCountsAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize,
        User? currentUser);
    Task<Module> CreateAsync(Module module);
    Task<Module> UpdateAsync(int id, Module module);
    Task DeleteAsync(int id);
}
