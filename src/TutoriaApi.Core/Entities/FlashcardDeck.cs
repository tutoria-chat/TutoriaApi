namespace TutoriaApi.Core.Entities;

/// <summary>
/// A flashcard deck a B2C student generated on a topic. Its cards are regular
/// Flashcards (DeckId set) so reviews reuse FlashcardReviews + spaced repetition.
/// Module-wide institutional cards have DeckId = null.
/// </summary>
public class FlashcardDeck : BaseEntity
{
    public int StudentId { get; set; }
    public int ModuleId { get; set; }
    public Module? Module { get; set; }
    public required string Title { get; set; }
}
