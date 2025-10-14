using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IModuleService
{
    Task<Module?> GetByIdAsync(int id);
    Task<Module?> GetWithDetailsAsync(int id);
    Task<(IEnumerable<Module> Items, int Total)> GetPagedAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize);
    Task<Module> CreateAsync(Module module);
    Task<Module> UpdateAsync(int id, Module module);
    Task DeleteAsync(int id);
}
