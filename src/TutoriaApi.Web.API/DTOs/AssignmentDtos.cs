using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class AssignmentListDto
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AssignmentDetailDto : AssignmentListDto
{
    public string? DownloadUrl { get; set; }
}

public class AssignmentCreateRequest
{
    public int ModuleId { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Due date is required")]
    public DateTime DueDate { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;
}

public class AssignmentUpdateRequest
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Due date is required")]
    public DateTime DueDate { get; set; }
}
