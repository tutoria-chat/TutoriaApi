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
}
