namespace TutoriaApi.Core.Entities;

/// <summary>
/// A "import calendar events from a PDF" job. The professor uploads a file
/// (syllabus / schedule), the worker extracts candidate events with AI, the
/// professor reviews/edits them, then confirms — which creates CourseEvents.
/// Mirrors <see cref="QuizUploadJob"/>.
/// </summary>
public class CalendarImportJob : BaseEntity
{
    public int CourseId { get; set; }
    public required string Status { get; set; }   // pending | processing | completed | failed | confirmed
    public int ExtractedCount { get; set; }
    public int ConfirmedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>S3 key for the uploaded file.</summary>
    public string? InputS3Key { get; set; }

    /// <summary>Original file name uploaded by the professor.</summary>
    public string? OriginalFilename { get; set; }

    /// <summary>JSON array of extracted events stored for professor review.</summary>
    public string? ExtractedEventsJson { get; set; }

    public int CreatedByUserId { get; set; }

    // Navigation
    public Course Course { get; set; } = null!;
}
