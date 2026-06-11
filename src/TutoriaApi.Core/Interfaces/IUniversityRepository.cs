using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityRepository : IRepository<University>
{
    Task<University?> GetByNameAsync(string name);
    Task<University?> GetByCodeAsync(string code);
    Task<University?> GetByIdWithCoursesAsync(int id);
    Task<List<University>> GetByIdsAsync(List<int> ids);
    Task<bool> ExistsByNameAsync(string name);
    Task<bool> ExistsByCodeAsync(string code);
    Task<(IEnumerable<University> Items, int Total)> SearchAsync(string? search, int page, int pageSize);
    Task<int> GetProfessorsCountAsync(int universityId);
    Task<int> GetModulesCountByCourseAsync(int courseId);
    Task<int> GetAssignedProfessorsCountByCourseAsync(int courseId);
    Task<int> GetStudentsCountByCourseAsync(int courseId);
    /// <summary>Direct SQL update for HasAssignments — bypasses EF Core change tracking.</summary>
    Task<int> SetHasAssignmentsAsync(int id, bool hasAssignments);

    /// <summary>Direct SQL update for HasAIQuizzes — bypasses EF Core change tracking.</summary>
    Task<int> SetHasAIQuizzesAsync(int id, bool hasAIQuizzes);
}
