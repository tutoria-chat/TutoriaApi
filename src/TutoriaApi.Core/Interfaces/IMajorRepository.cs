using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IMajorRepository : IRepository<Major>
{
    Task<IEnumerable<Major>> GetByUniversityIdAsync(int universityId);
    Task<bool> ExistsByNameInUniversityAsync(string name, int universityId);
    /// <summary>Of the given major ids, those that actually belong to the university.</summary>
    Task<List<int>> GetValidMajorIdsAsync(IEnumerable<int> majorIds, int universityId);
    /// <summary>Insert several majors at once (skips SaveChanges per row).</summary>
    Task AddRangeAsync(IEnumerable<Major> majors);
    /// <summary>Lowercased set of existing major names in the university (seed idempotency).</summary>
    Task<HashSet<string>> ExistingNamesLowerAsync(int universityId);

    // Course tagging (CourseMajors join)
    Task<IEnumerable<Major>> GetMajorsForCourseAsync(int courseId);
    /// <summary>Majors per course id (for list/detail DTOs), keyed by course id.</summary>
    Task<Dictionary<int, List<Major>>> GetMajorsForCoursesAsync(IEnumerable<int> courseIds);
    /// <summary>Replace a course's majors with exactly the given set.</summary>
    Task SetCourseMajorsAsync(int courseId, IEnumerable<int> majorIds);
}
