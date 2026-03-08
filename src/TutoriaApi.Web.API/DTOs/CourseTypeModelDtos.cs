using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class CourseTypeModelDto
{
    public int Id { get; set; }
    public string CourseType { get; set; } = string.Empty;
    public int AIModelId { get; set; }
    public string? AIModelName { get; set; }
    public string? AIModelDisplayName { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CourseTypeModelCreateRequest
{
    [Required(ErrorMessage = "Course type is required")]
    [MaxLength(50, ErrorMessage = "Course type cannot exceed 50 characters")]
    public string CourseType { get; set; } = string.Empty;

    [Required(ErrorMessage = "AI model ID is required")]
    public int AIModelId { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CourseTypeModelUpdateRequest
{
    [Required(ErrorMessage = "Course type is required")]
    [MaxLength(50, ErrorMessage = "Course type cannot exceed 50 characters")]
    public string CourseType { get; set; } = string.Empty;

    [Required(ErrorMessage = "AI model ID is required")]
    public int AIModelId { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UniversityCourseTypeModelDto
{
    public int Id { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public string CourseType { get; set; } = string.Empty;
    public int AIModelId { get; set; }
    public string? AIModelName { get; set; }
    public string? AIModelDisplayName { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UniversityCourseTypeModelCreateRequest
{
    [Required(ErrorMessage = "Course type is required")]
    [MaxLength(50, ErrorMessage = "Course type cannot exceed 50 characters")]
    public string CourseType { get; set; } = string.Empty;

    [Required(ErrorMessage = "AI model ID is required")]
    public int AIModelId { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}
