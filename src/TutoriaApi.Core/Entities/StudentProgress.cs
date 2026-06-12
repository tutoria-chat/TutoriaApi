namespace TutoriaApi.Core.Entities;

/// <summary>
/// Per-student gamification rollup, updated transactionally as activities land.
/// Level and Tier are pure functions of TotalXp (recomputed on each update) but
/// stored denormalized so leaderboards and the widget read in one query.
/// </summary>
public class StudentProgress
{
    public int StudentId { get; set; }

    public int TotalXp { get; set; }
    public int Level { get; set; } = 1;

    /// <summary>bronze | silver | gold | platinum | diamond | crystal</summary>
    public string Tier { get; set; } = "bronze";

    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }

    /// <summary>Date (America/Sao_Paulo) of the most recent activity — drives streaks.</summary>
    public DateOnly? LastActivityDate { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
