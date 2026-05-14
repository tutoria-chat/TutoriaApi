namespace TutoriaApi.Core.Entities;

/// <summary>
/// Supplementary reference/context files professors attach to an assignment.
/// These are available to the AI when grading student submissions.
/// Separate from the main assignment document (Assignment.S3Key) and rubric.
/// </summary>
public class AssignmentContextFile
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public required string S3Key { get; set; }
    public required string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public required string ContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
