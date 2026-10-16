using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>Data access for the course lifecycle (duration, alerts, auto-removal).</summary>
public interface ICourseLifecycleRepository
{
    /// <summary>Courses the daily job looks at: not deleted, not deactivated, not B2C. University loaded.</summary>
    Task<List<Course>> GetActiveInstitutionCoursesAsync();

    /// <summary>Course by id (including deactivated ones; deleted excluded), University loaded.</summary>
    Task<Course?> GetCourseAsync(int courseId);

    Task SaveCourseAsync(Course course);

    /// <summary>Students currently enrolled (existing student accounts only — orphan rows don't count).</summary>
    Task<int> CountEnrolledStudentsAsync(int courseId);

    Task<DateTime?> GetEarliestEnrollmentAsync(int courseId);

    /// <summary>CreatedAt of the oldest active, unexpired module access key in the course; null if none.</summary>
    Task<DateTime?> GetOldestActiveKeyCreatedAtAsync(int courseId, DateTime nowUtc);

    /// <summary>Active professors assigned to the course, with an email.</summary>
    Task<List<User>> GetAssignedProfessorsAsync(int courseId);

    Task<bool> IsProfessorAssignedAsync(int courseId, int professorId);

    Task<CourseLifecycleNotification?> GetNotificationAsync(int courseId, string key);

    Task AddNotificationAsync(CourseLifecycleNotification notification);

    /// <summary>Unenrolls every student from the course; returns how many enrollments were removed.</summary>
    Task<int> RemoveAllStudentsAsync(int courseId);
}
