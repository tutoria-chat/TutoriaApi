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

    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}
