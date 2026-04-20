namespace TutoriaApi.Core.Entities;

public class AssignmentSubmission : BaseEntity
{
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public required string StudentId { get; set; }
    public required string S3Key { get; set; }
    public required string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public required string ContentType { get; set; }
    public DateTime SubmittedAt { get; set; }

    // Nullable until auto-grading is built
    public string Status { get; set; } = "submitted";
    public decimal? Grade { get; set; }
    public string? GradingNotes { get; set; }
    public DateTime? GradedAt { get; set; }
    public int? GradedByUserId { get; set; }
}
