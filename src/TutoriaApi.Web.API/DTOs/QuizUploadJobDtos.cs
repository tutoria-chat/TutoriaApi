using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class QuizUploadJobDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public required string Status { get; set; }
    public int ExtractedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OriginalFilename { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateQuizUploadJobRequest
{
    [Required]
    public int CourseId { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}
