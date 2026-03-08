using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class PlanDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPriceBRL { get; set; }
    public string? StripePriceId { get; set; }
    public int MaxCourses { get; set; }
    public int MaxModules { get; set; }
    public int? MaxStudents { get; set; }
    public bool HasAIQuizzes { get; set; }
    public bool HasWhatsApp { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasCustomModelConfig { get; set; }
    public int TrialDays { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsCustom { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PlanCreateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required")]
    [MaxLength(100, ErrorMessage = "Slug cannot exceed 100 characters")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Monthly price must be non-negative")]
    public decimal MonthlyPriceBRL { get; set; }

    [MaxLength(255, ErrorMessage = "Stripe price ID cannot exceed 255 characters")]
    public string? StripePriceId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max courses must be non-negative")]
    public int MaxCourses { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max modules must be non-negative")]
    public int MaxModules { get; set; }

    public int? MaxStudents { get; set; }

    public bool HasAIQuizzes { get; set; }
    public bool HasWhatsApp { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasCustomModelConfig { get; set; }

    [Range(0, 365, ErrorMessage = "Trial days must be between 0 and 365")]
    public int TrialDays { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCustom { get; set; }
}

public class PlanUpdateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required")]
    [MaxLength(100, ErrorMessage = "Slug cannot exceed 100 characters")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Monthly price must be non-negative")]
    public decimal MonthlyPriceBRL { get; set; }

    [MaxLength(255, ErrorMessage = "Stripe price ID cannot exceed 255 characters")]
    public string? StripePriceId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max courses must be non-negative")]
    public int MaxCourses { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Max modules must be non-negative")]
    public int MaxModules { get; set; }

    public int? MaxStudents { get; set; }

    public bool HasAIQuizzes { get; set; }
    public bool HasWhatsApp { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasCustomModelConfig { get; set; }

    [Range(0, 365, ErrorMessage = "Trial days must be between 0 and 365")]
    public int TrialDays { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCustom { get; set; }
}
