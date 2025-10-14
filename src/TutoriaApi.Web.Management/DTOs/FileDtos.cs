using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.Management.DTOs;

public class FileListDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public string? OpenAIFileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FileDetailDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public string? OpenAIFileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateFileStatusRequest
{
    [Required(ErrorMessage = "Status is required")]
    [RegularExpression("^(pending|processing|completed|failed)$",
        ErrorMessage = "Status must be: pending, processing, completed, or failed")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Error message cannot exceed 500 characters")]
    public string? ErrorMessage { get; set; }

    [MaxLength(255, ErrorMessage = "OpenAI File ID cannot exceed 255 characters")]
    public string? OpenAIFileId { get; set; }
}
