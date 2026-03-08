namespace TutoriaApi.Web.API.DTOs;

public class StudentImportResultDto
{
    public int JobId { get; set; }
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int EnrolledCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<StudentImportErrorDto> Errors { get; set; } = new();
}

public class StudentImportErrorDto
{
    public int Row { get; set; }
    public string Matricula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class StudentImportJobDto
{
    public int Id { get; set; }
    public int UniversityId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public string UploadedByUsername { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int EnrolledCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorDetails { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
