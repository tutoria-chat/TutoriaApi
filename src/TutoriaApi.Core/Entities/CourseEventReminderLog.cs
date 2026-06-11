namespace TutoriaApi.Core.Entities;

/// <summary>
/// Dedupe record for reminder emails: one row per (event, reminder slot),
/// written when the Hangfire reminder job sends that slot's batch.
/// </summary>
public class CourseEventReminderLog : BaseEntity
{
    public int CourseEventId { get; set; }
    public CourseEvent? CourseEvent { get; set; }

    /// <summary>7d | 3d | 2d | 24h</summary>
    public required string ReminderKey { get; set; }

    public DateTime SentAt { get; set; }
    public int RecipientsCount { get; set; }
}
