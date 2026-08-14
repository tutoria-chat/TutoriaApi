using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class AssignmentContextFileDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
}

public class AssignmentListDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string[] Keywords { get; set; } = [];
    public string? GradingCriteria { get; set; }
    public string? RubricOriginalFileName { get; set; }
    public int CreatedByUserId { get; set; }
    public List<AssignmentContextFileDto> ContextFiles { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AssignmentDetailDto : AssignmentListDto
{
    public string? DownloadUrl { get; set; }
    public string? RubricDownloadUrl { get; set; }
}

public class AssignmentCreateRequest
{
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Due date is required")]
    public DateTime DueDate { get; set; }

    /// Comma-separated keywords (e.g. "argumentação,estrutura,referências")
    public string? Keywords { get; set; }

    /// <summary>Grading criteria / rubric description shown to students and used by AI for grading.</summary>
    [MaxLength(2000)]
    public string? GradingCriteria { get; set; }

    [Required(ErrorMessage = "Assignment file is required")]
    public IFormFile File { get; set; } = null!;

    /// Optional rubric / evaluation criteria file
    public IFormFile? RubricFile { get; set; }

    /// Optional supplementary context files (reference material for AI grading)
    public List<IFormFile>? ContextFiles { get; set; }
}

public class AssignmentUpdateRequest
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Due date is required")]
    public DateTime DueDate { get; set; }

    /// Comma-separated keywords
    public string? Keywords { get; set; }

    /// <summary>Grading criteria / rubric description.</summary>
    [MaxLength(2000)]
    public string? GradingCriteria { get; set; }
}
