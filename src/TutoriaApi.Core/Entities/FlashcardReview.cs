namespace TutoriaApi.Core.Entities;

/// <summary>
/// Per-student spaced-repetition state for one flashcard (SM-2-lite). One row
/// per (student, flashcard); a card with no row yet is treated as "new/due".
/// Written by tutoria-api as the student grades reviews.
/// </summary>
public class FlashcardReview : BaseEntity
{
    public int StudentId { get; set; }
    public int FlashcardId { get; set; }
    public Flashcard? Flashcard { get; set; }

    /// <summary>Consecutive successful recalls (resets to 0 on "again").</summary>
    public int Repetitions { get; set; }

    /// <summary>SM-2 ease factor; starts at 2.5, floored at 1.3.</summary>
    public double EaseFactor { get; set; } = 2.5;

    /// <summary>Current interval in days until the card is due again.</summary>
    public int IntervalDays { get; set; }

    public DateTime DueAt { get; set; }
    public DateTime LastReviewedAt { get; set; }
}
