using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseRepository : IRepository<Course>
{
    Task<Course?> GetWithDetailsAsync(int id);
    Task<IEnumerable<Course>> GetByUniversityIdAsync(int universityId);
    Task<(IEnumerable<Course> Items, int Total)> SearchAsync(int? universityId, string? search, int page, int pageSize);
    Task<bool> ExistsByCodeAndUniversityAsync(string code, int universityId);
}
