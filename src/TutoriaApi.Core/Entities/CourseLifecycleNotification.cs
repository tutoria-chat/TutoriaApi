namespace TutoriaApi.Core.Entities;

/// <summary>
/// One lifecycle email batch sent to a course's professors. The unique
/// (CourseId, Key) index makes the daily job idempotent: "unused:2026-10-16"
/// is the alert for that day, "warn:2026-11-02" the 7-day warning for a
/// removal scheduled on that date, "removed:2026-11-02" the removal itself.
/// </summary>
public class CourseLifecycleNotification : BaseEntity
{
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public required string Key { get; set; }
    public DateTime SentAt { get; set; }
    public int RecipientsCount { get; set; }
}
