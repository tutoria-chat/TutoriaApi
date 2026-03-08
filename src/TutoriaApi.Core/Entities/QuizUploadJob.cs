namespace TutoriaApi.Core.Entities;

public class QuizUploadJob : BaseEntity
{
    public int ModuleId { get; set; }
    public int? FileId { get; set; }
    public required string Status { get; set; }
    public int ExtractedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
