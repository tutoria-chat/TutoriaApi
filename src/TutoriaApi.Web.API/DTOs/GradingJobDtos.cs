using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class GradingJobDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int ProcessedSubmissions { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool HasResult { get; set; }
    public string? GradingCriteria { get; set; }
    public string? OriginalFilename { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateGradingJobRequest
{
    [Required(ErrorMessage = "Course ID is required")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>Optional grading criteria the AI should focus on (max 2000 chars).</summary>
    [MaxLength(2000)]
    public string? GradingCriteria { get; set; }
}
