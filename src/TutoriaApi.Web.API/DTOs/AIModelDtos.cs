using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class AIModelListDto
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }
    public int RequiredTier { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeprecated { get; set; }
    public string? RecommendedFor { get; set; }
    public int ModulesCount { get; set; }
}

public class AIModelDetailDto
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }
    public int RequiredTier { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeprecated { get; set; }
    public DateTime? DeprecationDate { get; set; }
    public string? Description { get; set; }
    public string? RecommendedFor { get; set; }
    public int ModulesCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AIModelCreateRequest
{
    [Required(ErrorMessage = "Model name is required")]
    [MaxLength(100, ErrorMessage = "Model name cannot exceed 100 characters")]
    public string ModelName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required")]
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Provider is required")]
    [MaxLength(50, ErrorMessage = "Provider cannot exceed 50 characters")]
    [RegularExpression("^(openai|anthropic)$", ErrorMessage = "Provider must be either 'openai' or 'anthropic'")]
    public string Provider { get; set; } = string.Empty;

    [Required(ErrorMessage = "Max tokens is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Max tokens must be greater than 0")]
    public int MaxTokens { get; set; }

    public bool SupportsVision { get; set; } = false;

    public bool SupportsFunctionCalling { get; set; } = false;

    [Range(0, double.MaxValue, ErrorMessage = "Input cost must be non-negative")]
    public decimal? InputCostPer1M { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Output cost must be non-negative")]
    public decimal? OutputCostPer1M { get; set; }

    [Range(1, 3, ErrorMessage = "Required tier must be between 1 and 3")]
    public int RequiredTier { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public bool IsDeprecated { get; set; } = false;

    public DateTime? DeprecationDate { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [MaxLength(200, ErrorMessage = "RecommendedFor cannot exceed 200 characters")]
    public string? RecommendedFor { get; set; }
}

public class AIModelUpdateRequest
{
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string? DisplayName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Max tokens must be greater than 0")]
    public int? MaxTokens { get; set; }

    public bool? SupportsVision { get; set; }

    public bool? SupportsFunctionCalling { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Input cost must be non-negative")]
    public decimal? InputCostPer1M { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Output cost must be non-negative")]
    public decimal? OutputCostPer1M { get; set; }

    [Range(1, 3, ErrorMessage = "Required tier must be between 1 and 3")]
    public int? RequiredTier { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsDeprecated { get; set; }

    public DateTime? DeprecationDate { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [MaxLength(200, ErrorMessage = "RecommendedFor cannot exceed 200 characters")]
    public string? RecommendedFor { get; set; }
}

public class AIModelDto
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeprecated { get; set; }
}
