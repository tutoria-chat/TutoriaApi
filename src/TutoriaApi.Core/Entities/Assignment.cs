namespace TutoriaApi.Core.Entities;

public class Assignment : BaseEntity
{
    public int ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public required string Title { get; set; }
    public string? Description { get; set; }

    public DateTime DueDate { get; set; }
    public bool IsPublished { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // S3 storage — separate from File entity to avoid triggering RAG/quiz pipelines
    public required string S3Key { get; set; }
    public required string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public required string ContentType { get; set; }

    // Comma-separated keywords the AI should focus on when giving feedback
    public string? Keywords { get; set; }

    // Optional rubric file (evaluation criteria uploaded by professor)
    public string? RubricS3Key { get; set; }
    public string? RubricOriginalFileName { get; set; }
    public long? RubricFileSizeBytes { get; set; }
    public string? RubricContentType { get; set; }

    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();

    /// <summary>Supplementary reference files professors upload alongside the assignment.</summary>
    public ICollection<AssignmentContextFile> ContextFiles { get; set; } = new List<AssignmentContextFile>();
}
