using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

// View model for course with counts
public class CourseWithCountsViewModel
{
    public Course Course { get; set; } = null!;
    public string? UniversityName { get; set; }
    public int ModulesCount { get; set; }
    public int ProfessorsCount { get; set; }
    public int StudentsCount { get; set; }
}

// View model for course with full details
public class CourseDetailViewModel
{
    public Course Course { get; set; } = null!;
    public University? University { get; set; }
    public List<Module> Modules { get; set; } = new();
    public Dictionary<int, int> ModuleFileCounts { get; set; } = new();
    public Dictionary<int, int> ModuleTokenCounts { get; set; } = new();
    public List<User> Students { get; set; } = new();
}

public interface ICourseService
{
    Task<CourseWithCountsViewModel?> GetCourseWithCountsAsync(int id);
    Task<CourseDetailViewModel?> GetCourseWithFullDetailsAsync(int id);
    Task<(List<CourseWithCountsViewModel> Items, int Total)> GetPagedWithCountsAsync(
        int? universityId,
        string? search,
        int page,
        int pageSize);
    Task<Course> CreateAsync(Course course);
    Task<CourseWithCountsViewModel> UpdateAsync(int id, Course course);
    Task DeleteAsync(int id);
    Task AssignProfessorAsync(int courseId, int professorId);
    Task UnassignProfessorAsync(int courseId, int professorId);
}
