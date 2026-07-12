using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

// View model for university with enriched course data
public class UniversityWithCoursesViewModel
{
    public University University { get; set; } = null!;
    public int ProfessorsCount { get; set; }
    public List<CourseViewModel> Courses { get; set; } = new();
}

public class CourseViewModel
{
    public Course Course { get; set; } = null!;
    public int ModulesCount { get; set; }
    public int ProfessorsCount { get; set; }
    public int StudentsCount { get; set; }
}

public interface IUniversityService
{
    Task<University?> GetByIdAsync(int id);
    Task<University?> GetWithCoursesAsync(int id);
    Task<UniversityWithCoursesViewModel?> GetUniversityWithDetailsAsync(int id);
    Task<(IEnumerable<University> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize);
    Task<University> CreateAsync(University university, User currentUser);
    Task<University> UpdateAsync(int id, University university, User currentUser);

    /// <summary>Normalized trusted browser origins for an institution (CORS allowlist).</summary>
    Task<List<string>> GetAllowedOriginsAsync(int id, User currentUser);

    /// <summary>Replace an institution's trusted origins (normalized + de-duplicated). Returns the saved set.</summary>
    Task<List<string>> UpdateAllowedOriginsAsync(int id, IEnumerable<string> origins, User currentUser);
    Task DeleteAsync(int id, User currentUser);
    Task<int> GetProfessorsCountAsync(int universityId);
    Task<int> GetModulesCountByCourseAsync(int courseId);
    Task<int> GetAssignedProfessorsCountByCourseAsync(int courseId);
    Task<int> GetStudentsCountByCourseAsync(int courseId);
}
