using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IModuleRepository : IRepository<Module>
{
    Task<Module?> GetWithDetailsAsync(int id);
    Task<IEnumerable<Module>> GetByCourseIdAsync(int courseId);
    Task<(IEnumerable<Module> Items, int Total)> SearchAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize);
    Task<bool> ExistsByCodeAndCourseAsync(string code, int courseId);
}
