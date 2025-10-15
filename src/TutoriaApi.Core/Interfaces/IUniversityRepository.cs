using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityRepository : IRepository<University>
{
    Task<University?> GetByNameAsync(string name);
    Task<University?> GetByCodeAsync(string code);
    Task<University?> GetByIdWithCoursesAsync(int id);
    Task<bool> ExistsByNameAsync(string name);
    Task<bool> ExistsByCodeAsync(string code);
    Task<(IEnumerable<University> Items, int Total)> SearchAsync(string? search, int page, int pageSize);
}
