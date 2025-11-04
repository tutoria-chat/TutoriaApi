namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Repository for managing Professor-Course junction table relationships
/// </summary>
public interface IProfessorCourseRepository
{
    /// <summary>
    /// Get all course IDs assigned to a professor
    /// </summary>
    Task<List<int>> GetCourseIdsByProfessorIdAsync(int professorId);

    /// <summary>
    /// Get all professor IDs assigned to a course
    /// </summary>
    Task<List<int>> GetProfessorIdsByCourseIdAsync(int courseId);

    /// <summary>
    /// Get course counts grouped by professor IDs
    /// </summary>
    Task<Dictionary<int, int>> GetCourseCountsByProfessorIdsAsync(IEnumerable<int> professorIds);

    /// <summary>
    /// Check if a professor is assigned to a specific course
    /// </summary>
    Task<bool> IsProfessorAssignedToCourseAsync(int professorId, int courseId);

    /// <summary>
    /// Assign a professor to a course
    /// </summary>
    Task AddProfessorToCourseAsync(int professorId, int courseId);

    /// <summary>
    /// Remove a professor from a course
    /// </summary>
    Task RemoveProfessorFromCourseAsync(int professorId, int courseId);

    /// <summary>
    /// Remove all course assignments for a professor
    /// </summary>
    Task RemoveAllCoursesForProfessorAsync(int professorId);

    /// <summary>
    /// Remove all professor assignments for a course
    /// </summary>
    Task RemoveAllProfessorsForCourseAsync(int courseId);
}
