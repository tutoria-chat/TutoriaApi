namespace TutoriaApi.Core.Entities;

public class QuizUploadJob : BaseEntity
{
    public int ModuleId { get; set; }
    public int? FileId { get; set; }
    public required string Status { get; set; }
    public int ExtractedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>S3 key for the uploaded quiz file (async flow).</summary>
    public string? InputS3Key { get; set; }

    /// <summary>Original file name uploaded by the professor.</summary>
    public string? OriginalFilename { get; set; }

    /// <summary>JSON array of extracted questions stored for professor review (async flow).</summary>
    public string? ExtractedQuestionsJson { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
