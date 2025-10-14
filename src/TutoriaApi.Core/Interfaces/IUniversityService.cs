using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityService
{
    Task<University?> GetByIdAsync(int id);
    Task<University?> GetWithCoursesAsync(int id);
    Task<(IEnumerable<University> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize);
    Task<University> CreateAsync(University university);
    Task<University> UpdateAsync(int id, University university);
    Task DeleteAsync(int id);
}
