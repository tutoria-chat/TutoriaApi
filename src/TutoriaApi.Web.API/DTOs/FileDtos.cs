using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class FileListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? BlobUrl { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public bool IsActive { get; set; }
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class FileDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? BlobUrl { get; set; }
    public string? BlobContainer { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public bool IsActive { get; set; }
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UploadFileRequest
{
    [Required(ErrorMessage = "Module ID is required")]
    public int ModuleId { get; set; }

    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;
}

public class UpdateFileRequest
{
    [MaxLength(255, ErrorMessage = "File name cannot exceed 255 characters")]
    public string? FileName { get; set; }
}

public class UpdateFileStatusRequest
{
    [Required(ErrorMessage = "IsActive is required")]
    public bool IsActive { get; set; }

    [MaxLength(255, ErrorMessage = "OpenAI File ID cannot exceed 255 characters")]
    public string? OpenAIFileId { get; set; }

    [MaxLength(255, ErrorMessage = "Anthropic File ID cannot exceed 255 characters")]
    public string? AnthropicFileId { get; set; }
}
