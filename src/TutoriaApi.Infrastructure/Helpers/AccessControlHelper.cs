using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Helpers;

/// <summary>
/// Helper methods for access control checks across the application.
/// Implements multi-tenant security rules for professors and admin professors.
/// </summary>
public class AccessControlHelper
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<AccessControlHelper> _logger;
    private const int MaxCourseAssignments = 1000; // Reasonable limit for professor course assignments

    public AccessControlHelper(TutoriaDbContext context, ILogger<AccessControlHelper> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a professor is assigned to a specific course.
    /// </summary>
    /// <param name="professorId">The professor's user ID</param>
    /// <param name="courseId">The course ID to check</param>
    /// <returns>True if the professor is assigned to the course</returns>
    public async Task<bool> IsProfessorAssignedToCourseAsync(int professorId, int courseId)
    {
        return await _context.ProfessorCourses
            .AnyAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);
    }

    /// <summary>
    /// Checks if a professor has access to a module (via course assignment).
    /// </summary>
    /// <param name="professorId">The professor's user ID</param>
    /// <param name="moduleId">The module ID to check</param>
    /// <returns>True if the professor is assigned to the module's course</returns>
    public async Task<bool> IsProfessorAssignedToModuleAsync(int professorId, int moduleId)
    {
        var module = await _context.Modules.FindAsync(moduleId);
        if (module == null) return false;

        return await IsProfessorAssignedToCourseAsync(professorId, module.CourseId);
    }

    /// <summary>
    /// Gets the university ID for a course.
    /// </summary>
    /// <param name="courseId">The course ID</param>
    /// <returns>The university ID, or null if course not found</returns>
    public async Task<int?> GetCourseUniversityIdAsync(int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        return course?.UniversityId;
    }

    /// <summary>
    /// Gets the university ID for a module.
    /// </summary>
    /// <param name="moduleId">The module ID</param>
    /// <returns>The university ID, or null if module/course not found</returns>
    public async Task<int?> GetModuleUniversityIdAsync(int moduleId)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        return module?.Course?.UniversityId;
    }

    /// <summary>
    /// Checks if a file belongs to a professor's assigned courses.
    /// </summary>
    /// <param name="professorId">The professor's user ID</param>
    /// <param name="fileId">The file ID to check</param>
    /// <returns>True if the file belongs to one of the professor's assigned courses</returns>
    public async Task<bool> IsProfessorAssignedToFileAsync(int professorId, int fileId)
    {
        var file = await _context.Files
            .Include(f => f.Module)
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null) return false;

        return await IsProfessorAssignedToCourseAsync(professorId, file.Module.CourseId);
    }

    /// <summary>
    /// Gets all course IDs assigned to a professor (limited to prevent unbounded queries).
    /// </summary>
    /// <param name="professorId">The professor's user ID</param>
    /// <returns>List of course IDs (maximum 1000 courses)</returns>
    public async Task<List<int>> GetProfessorCourseIdsAsync(int professorId)
    {
        var courseIds = await _context.ProfessorCourses
            .Where(pc => pc.ProfessorId == professorId)
            .Select(pc => pc.CourseId)
            .Take(MaxCourseAssignments)
            .ToListAsync();

        if (courseIds.Count == MaxCourseAssignments)
        {
            _logger.LogWarning(
                "Professor {ProfessorId} has reached the maximum course assignment limit of {MaxLimit}. " +
                "This may indicate a data issue or misconfiguration.",
                professorId,
                MaxCourseAssignments);
        }

        return courseIds;
    }
}
