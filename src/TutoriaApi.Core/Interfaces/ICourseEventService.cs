using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseEventService
{
    /// <summary>
    /// All calendar entries for a course: explicit CourseEvents plus published
    /// assignments that have no linked event (synthesized, read-only).
    /// </summary>
    Task<(List<CourseEvent> Events, List<Assignment> UnlinkedAssignments)> GetCourseCalendarAsync(
        int courseId, DateTime? fromUtc, DateTime? toUtc, User currentUser);

    Task<CourseEvent> CreateAsync(CourseEvent courseEvent, User currentUser);
    Task<CourseEvent> UpdateAsync(int id, CourseEvent updated, User currentUser);
    Task DeleteAsync(int id, User currentUser);
}
