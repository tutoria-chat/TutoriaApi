using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseRepository : IRepository<Course>
{
    Task<Course?> GetWithDetailsAsync(int id);
    Task<Course?> GetWithFullDetailsAsync(int id);
    Task<IEnumerable<Course>> GetByUniversityIdAsync(int universityId);
    Task<(IEnumerable<Course> Items, int Total)> SearchAsync(int? universityId, int? professorId, string? search, int page, int pageSize);
    Task<bool> ExistsByCodeAndUniversityAsync(string code, int universityId);

    // Count methods
    Task<int> GetModulesCountAsync(int courseId);
    Task<int> GetProfessorsCountAsync(int courseId);
    Task<int> GetStudentsCountAsync(int courseId);

    // Professor assignment
    Task<bool> IsProfessorAssignedAsync(int courseId, int professorId);
    Task AssignProfessorAsync(int courseId, int professorId);
    Task UnassignProfessorAsync(int courseId, int professorId);
}
