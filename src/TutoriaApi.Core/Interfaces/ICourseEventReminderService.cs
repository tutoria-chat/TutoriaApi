namespace TutoriaApi.Core.Interfaces;

public interface ICourseEventReminderService
{
    /// <summary>
    /// Send pending reminder emails for upcoming course events.
    /// Runs hourly via Hangfire; deduped per (event, slot) via CourseEventReminderLogs.
    /// </summary>
    Task ProcessRemindersAsync();
}
