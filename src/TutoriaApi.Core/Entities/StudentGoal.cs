namespace TutoriaApi.Core.Entities;

/// <summary>
/// A personal academic goal a student sets for themselves (e.g. "reach level 10",
/// "keep a 10-day streak", "complete 20 quizzes"). Progress is derived on read
/// from the gamification ledger / progress rollup — this row only stores the
/// target, so no status sync is ever needed.
/// </summary>
public class StudentGoal : BaseEntity
{
    public int StudentId { get; set; }

    /// <summary>level | xp | streak | quizzes | questions | flashcards</summary>
    public required string Metric { get; set; }

    public int TargetValue { get; set; }

    /// <summary>Optional course scope (null = across all courses).</summary>
    public int? CourseId { get; set; }
}
