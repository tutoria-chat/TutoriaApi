namespace TutoriaApi.Core.Entities;

public class StudentImportJob : BaseEntity
{
    public int UniversityId { get; set; }
    public int CourseId { get; set; }
    public int UploadedByUserId { get; set; }
    public required string FileName { get; set; }

    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int EnrolledCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorDetails { get; set; } // JSON array of row-level errors

    public DateTime? ProcessedAt { get; set; }

    // Navigation properties
    public University University { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
