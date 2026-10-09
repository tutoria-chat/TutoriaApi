namespace TutoriaApi.Core.Entities;

/// <summary>
/// ENEM redação reviewed by AI for a B2C student (Max plan). The result carries a
/// SUGGESTED score with an explicit AI disclaimer — this exists only in the
/// consumer app, never in the institutional product (CNE ban on AI grading).
/// </summary>
public class EssayReview : BaseEntity
{
    public int UserId { get; set; }
    public required string Theme { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>typed | photo</summary>
    public string Source { get; set; } = "typed";
    /// <summary>queued | processing | done | failed</summary>
    public string Status { get; set; } = "queued";
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Hidden by the student; still counts toward the weekly quota.</summary>
    public DateTime? DeletedAt { get; set; }
}
