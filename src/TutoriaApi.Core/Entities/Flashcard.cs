namespace TutoriaApi.Core.Entities;

/// <summary>
/// AI-generated flashcard for a module, built once from the module's extracted
/// content (PDFs, lecture files, summaries) and served to every student —
/// near-zero marginal cost. Future XP/gamification hooks attach here.
/// </summary>
public class Flashcard : BaseEntity
{
    public int ModuleId { get; set; }
    public Module? Module { get; set; }

    public required string FrontText { get; set; }
    public required string BackText { get; set; }

    /// <summary>Concept/topic label, aligned with quiz concepts when possible.</summary>
    public string? Concept { get; set; }

    /// <summary>easy | medium | hard</summary>
    public string Difficulty { get; set; } = "medium";

    /// <summary>ai_generated | manual</summary>
    public string Source { get; set; } = "ai_generated";

    public bool IsActive { get; set; } = true;

    /// <summary>Set for cards of a student's own deck (B2C app); null = module-wide card.</summary>
    public int? DeckId { get; set; }
}
