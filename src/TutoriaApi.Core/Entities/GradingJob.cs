namespace TutoriaApi.Core.Entities;

public class GradingJob : BaseEntity
{
    public int CourseId { get; set; }

    /// <summary>Staff user who created the job. Null for jobs started via the
    /// external automation API (authenticated by API key, no user).</summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>How the job was created: "dashboard" (default) or "external_api".</summary>
    public string? Source { get; set; }

    /// <summary>pending | processing | completed | failed</summary>
    public required string Status { get; set; }

    /// <summary>Original filename of the uploaded JSON (for display).</summary>
    public string? OriginalFilename { get; set; }

    /// <summary>S3 key for the uploaded student-answers JSON.</summary>
    public string? InputS3Key { get; set; }

    /// <summary>S3 key for the generated grading CSV.</summary>
    public string? ResultS3Key { get; set; }

    public int TotalSubmissions { get; set; }
    public int ProcessedSubmissions { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Optional professor-defined criteria the AI should focus on when grading.
    /// Injected into the AI system prompt.
    /// </summary>
    public string? GradingCriteria { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public User? CreatedBy { get; set; }
}
