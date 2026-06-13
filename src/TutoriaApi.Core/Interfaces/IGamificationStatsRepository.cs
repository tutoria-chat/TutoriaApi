namespace TutoriaApi.Core.Interfaces;

/// <summary>Per-course activity aggregate from the gamification ledger.</summary>
public class CourseActivityRow
{
    public int CourseId { get; set; }
    public int TotalXp { get; set; }
    public int ActiveStudents { get; set; }
    public int Questions { get; set; }
    public int Quizzes { get; set; }
    public int Flashcards { get; set; }
}

/// <summary>Per-module activity aggregate from the gamification ledger.</summary>
public class ModuleActivityRow
{
    public int ModuleId { get; set; }
    public int TotalXp { get; set; }
    public int ActiveStudents { get; set; }
    public int Questions { get; set; }
    public int Quizzes { get; set; }
    public int Flashcards { get; set; }
}

/// <summary>A student whose active streak is alive but not yet extended today.</summary>
public class StreakAtRiskRow
{
    public int StudentId { get; set; }
    public int StreakDays { get; set; }
}

/// <summary>A leaderboard row for the rankings/highlights views (real student names — staff-only).</summary>
public class RankingPerformerRow
{
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalXp { get; set; }
    public int Level { get; set; }
    public string Tier { get; set; } = "bronze";
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public string? DisplayedTitleKey { get; set; }
}

/// <summary>A student + a single ranked metric value (xp gained, streak, activity count, badges…).</summary>
public class RankingValueRow
{
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string? DisplayedTitleKey { get; set; }
}

/// <summary>Aggregate engagement counts over a window for a set of students.</summary>
public class EngagementTotalsRow
{
    public int Questions { get; set; }
    public int Quizzes { get; set; }
    public int Flashcards { get; set; }
    public int StudyPlans { get; set; }
    public int ActiveStudents { get; set; }
}

/// <summary>
/// Read-only aggregation over the gamification ledger (StudentActivities /
/// StudentProgress) for institution/class/discipline statistics. The ledger is
/// written by tutoria-api (Python); this side only reads it.
/// </summary>
public interface IGamificationStatsRepository
{
    Task<List<CourseActivityRow>> GetCourseActivityAsync(List<int> courseIds, DateTime sinceUtc);
    Task<List<ModuleActivityRow>> GetModuleActivityAsync(int courseId, DateTime sinceUtc);
    Task<Dictionary<int, int>> GetLevelsByStudentIdsAsync(List<int> studentIds);

    /// <summary>
    /// The equipped title key (StudentProgress.DisplayedTitleKey) per student, for
    /// the students that have one set. Used to show titles in admin/professor views.
    /// </summary>
    Task<Dictionary<int, string>> GetDisplayedTitleKeysByStudentIdsAsync(List<int> studentIds);

    /// <summary>
    /// Students whose last activity was on <paramref name="lastActiveDate"/> (i.e.
    /// streak alive but not extended since) with a streak of at least
    /// <paramref name="minStreak"/> days — candidates for a streak-saver nudge.
    /// </summary>
    Task<List<StreakAtRiskRow>> GetStreakAtRiskAsync(DateOnly lastActiveDate, int minStreak);

    // ---- Rankings / highlights (staff-facing) ----

    /// <summary>Distinct student IDs enrolled in any of the given courses.</summary>
    Task<List<int>> GetEnrolledStudentIdsAsync(List<int> courseIds);

    /// <summary>Top students by total XP (the rollup), with name/level/tier/streak/title.</summary>
    Task<List<RankingPerformerRow>> GetTopPerformersAsync(List<int> studentIds, int limit);

    /// <summary>Top students by XP gained within [startUtc, endUtc] (effort over the period).</summary>
    Task<List<RankingValueRow>> GetMostImprovedAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc, int limit);

    /// <summary>Top students by longest all-time streak (days).</summary>
    Task<List<RankingValueRow>> GetLongestStreaksAsync(List<int> studentIds, int limit);

    /// <summary>Top students by number of logged activities within [startUtc, endUtc].</summary>
    Task<List<RankingValueRow>> GetMostActiveAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc, int limit);

    /// <summary>Top students by number of badges earned (all-time).</summary>
    Task<List<RankingValueRow>> GetMostBadgesAsync(List<int> studentIds, int limit);

    /// <summary>Aggregate engagement counts (questions/quizzes/flashcards/plans/active) over a window.</summary>
    Task<EngagementTotalsRow> GetEngagementTotalsAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc);
}
