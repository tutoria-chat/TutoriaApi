using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseService
{
    Task<Course?> GetByIdAsync(int id);
    Task<Course?> GetWithDetailsAsync(int id);
    Task<(IEnumerable<Course> Items, int Total)> GetPagedAsync(int? universityId, string? search, int page, int pageSize);
    Task<Course> CreateAsync(Course course);
    Task<Course> UpdateAsync(int id, Course course);
    Task DeleteAsync(int id);
    Task AssignProfessorAsync(int courseId, int professorId);
    Task UnassignProfessorAsync(int courseId, int professorId);
}
