namespace TutoriaApi.Core.Entities;

/// <summary>
/// A badge/achievement a student has earned. The catalog (keys, names, icons)
/// lives in code/i18n; this table only records who earned what and when.
/// Unique per (StudentId, BadgeKey) so a badge is awarded at most once.
/// </summary>
public class StudentBadge : BaseEntity
{
    public int StudentId { get; set; }
    public required string BadgeKey { get; set; }
    public DateTime EarnedAt { get; set; }
}
