using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IMajorService
{
    Task<IEnumerable<Major>> GetByUniversityAsync(int universityId);
    Task<IEnumerable<Major>> GetForCourseAsync(int courseId);
    Task<Major> CreateAsync(int universityId, string name);
    Task DeleteAsync(int universityId, int majorId);
    /// <summary>Add any standard majors not already present; returns the full list afterwards.</summary>
    Task<IEnumerable<Major>> SeedDefaultsAsync(int universityId);
    /// <summary>Replace a course's majors, ignoring any ids that don't belong to the university.</summary>
    Task SetCourseMajorsAsync(int courseId, int universityId, IEnumerable<int> majorIds);
}
