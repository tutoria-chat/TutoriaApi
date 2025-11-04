namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Repository for managing Student-Course junction table relationships
/// </summary>
public interface IStudentCourseRepository
{
    /// <summary>
    /// Get all student IDs enrolled in a course
    /// </summary>
    Task<List<int>> GetStudentIdsByCourseIdAsync(int courseId);

    /// <summary>
    /// Get all course IDs a student is enrolled in
    /// </summary>
    Task<List<int>> GetCourseIdsByStudentIdAsync(int studentId);

    /// <summary>
    /// Check if a student is enrolled in a specific course
    /// </summary>
    Task<bool> IsStudentEnrolledInCourseAsync(int studentId, int courseId);

    /// <summary>
    /// Enroll a student in a course
    /// </summary>
    Task EnrollStudentInCourseAsync(int studentId, int courseId);

    /// <summary>
    /// Remove a student from a course
    /// </summary>
    Task RemoveStudentFromCourseAsync(int studentId, int courseId);

    /// <summary>
    /// Remove all course enrollments for a student
    /// </summary>
    Task RemoveAllCoursesForStudentAsync(int studentId);

    /// <summary>
    /// Remove all student enrollments for a course
    /// </summary>
    Task RemoveAllStudentsForCourseAsync(int courseId);
}
