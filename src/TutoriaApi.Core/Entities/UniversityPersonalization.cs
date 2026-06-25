namespace TutoriaApi.Core.Entities;

/// <summary>
/// Stores branding/appearance configuration for a university's widget.
/// One-to-one with University, created on first save, never deleted (just updated).
/// </summary>
public class UniversityPersonalization : BaseEntity
{
    public int UniversityId { get; set; }

    // Colors (stored as full hex strings: "#7C3AED")
    public string? PrimaryColor { get; set; }    // Send button + user message bubble
    public string? SecondaryColor { get; set; }  // Agent message bubble accent

    // Widget theme default: "light" | "dark" | "auto"
    public string DefaultTheme { get; set; } = "auto";

    // Chat bubble background opacity, 0–100 (%). Null = 100 (fully opaque).
    public int? BubbleOpacity { get; set; }

    // Navigation
    public University University { get; set; } = null!;
}
