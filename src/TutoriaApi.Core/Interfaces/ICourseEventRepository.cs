using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseEventRepository : IRepository<CourseEvent>
{
    Task<List<CourseEvent>> GetByCourseIdAsync(int courseId, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<CourseEvent?> GetByIdWithCourseAsync(int id);
    Task<CourseEvent?> GetByAssignmentIdAsync(int assignmentId);
    Task<List<int>> GetLinkedAssignmentIdsAsync(int courseId);

    /// <summary>Events starting within [fromUtc, toUtc] that have at least one reminder flag enabled.</summary>
    Task<List<CourseEvent>> GetUpcomingWithRemindersAsync(DateTime fromUtc, DateTime toUtc);

    Task<bool> HasReminderBeenSentAsync(int courseEventId, string reminderKey);
    Task AddReminderLogAsync(CourseEventReminderLog log);
}
