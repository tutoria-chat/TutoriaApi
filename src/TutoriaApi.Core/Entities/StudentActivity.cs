namespace TutoriaApi.Core.Entities;

/// <summary>
/// Append-only gamification ledger: one row per point-bearing academic action
/// (question asked, quiz finished, flashcards reviewed, study plan generated…).
/// Durable in SQL — unlike the 90-day DynamoDB chat/quiz logs — because XP must
/// be permanent. Tagged with course/module so the same ledger also feeds
/// per-class/discipline statistics later.
/// </summary>
public class StudentActivity : BaseEntity
{
    public int StudentId { get; set; }
    public int? CourseId { get; set; }
    public int? ModuleId { get; set; }

    /// <summary>chat_question | quiz_completed | flashcards_reviewed | study_plan_generated | ...</summary>
    public required string ActivityType { get; set; }

    public int Xp { get; set; }

    /// <summary>Optional trace back to the source row (quizId, planId, etc.).</summary>
    public string? ReferenceId { get; set; }

    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Idempotency/cap key (e.g. "chat:123:2026-06-12:3"). Unique when set, so
    /// daily caps and replays never double-award. NULL rows are never deduped.
    /// </summary>
    public string? DedupeKey { get; set; }
}
