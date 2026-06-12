namespace TutoriaApi.Core.Entities;

/// <summary>
/// A global ENEM/vestibular-style practice question, generated once per
/// knowledge area and shared across all institutions (ENEM is national and
/// standardized — not institution-specific), so the marginal AI cost per
/// student is zero. Written by tutoria-api's generation service.
/// </summary>
public class EnemQuestion : BaseEntity
{
    /// <summary>linguagens | matematica | natureza | humanas</summary>
    public required string Area { get; set; }

    /// <summary>The contextualized question stem (ENEM style).</summary>
    public required string Statement { get; set; }

    /// <summary>JSON array of 5 alternatives (A–E).</summary>
    public required string OptionsJson { get; set; }

    /// <summary>Index (0–4) of the correct alternative.</summary>
    public int CorrectIndex { get; set; }

    public string? Explanation { get; set; }

    /// <summary>easy | medium | hard</summary>
    public string Difficulty { get; set; } = "medium";

    public bool IsActive { get; set; } = true;
}
