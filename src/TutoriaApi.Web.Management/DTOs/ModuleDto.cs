using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.Management.DTOs;

public class ModuleListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Semester { get; set; }
    public int? Year { get; set; }
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int FilesCount { get; set; }
    public int TokensCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ModuleDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public int? Semester { get; set; }
    public int? Year { get; set; }
    public int CourseId { get; set; }
    public CourseDto? Course { get; set; }
    public string? OpenAIAssistantId { get; set; }
    public string? OpenAIVectorStoreId { get; set; }
    public DateTime? LastPromptImprovedAt { get; set; }
    public int PromptImprovementCount { get; set; }
    public string TutorLanguage { get; set; } = "pt-br";
    public List<FileDto> Files { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ModuleCreateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Code is required")]
    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "System prompt is required")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Range(1, 8, ErrorMessage = "Semester must be between 1 and 8")]
    public int? Semester { get; set; }

    [Range(2020, 2050, ErrorMessage = "Year must be between 2020 and 2050")]
    public int? Year { get; set; }

    [Required(ErrorMessage = "Course ID is required")]
    public int CourseId { get; set; }

    [MaxLength(10)]
    public string TutorLanguage { get; set; } = "pt-br";
}

public class ModuleUpdateRequest
{
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string? Code { get; set; }

    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [Range(1, 8, ErrorMessage = "Semester must be between 1 and 8")]
    public int? Semester { get; set; }

    [Range(2020, 2050, ErrorMessage = "Year must be between 2020 and 2050")]
    public int? Year { get; set; }

    [MaxLength(10)]
    public string? TutorLanguage { get; set; }
}

public class FileDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? OpenAIFileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
