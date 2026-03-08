using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class ProviderKeyListDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string MaskedKey { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProviderKeyDetailDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string MaskedKey { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProviderKeyCreateRequest
{
    [Required(ErrorMessage = "Provider is required")]
    [MaxLength(50, ErrorMessage = "Provider cannot exceed 50 characters")]
    public string Provider { get; set; } = string.Empty;

    [Required(ErrorMessage = "Key name is required")]
    [MaxLength(200, ErrorMessage = "Key name cannot exceed 200 characters")]
    public string KeyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Region cannot exceed 50 characters")]
    public string? Region { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProviderKeyUpdateRequest
{
    [Required(ErrorMessage = "Provider is required")]
    [MaxLength(50, ErrorMessage = "Provider cannot exceed 50 characters")]
    public string Provider { get; set; } = string.Empty;

    [Required(ErrorMessage = "Key name is required")]
    [MaxLength(200, ErrorMessage = "Key name cannot exceed 200 characters")]
    public string KeyName { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    [MaxLength(50, ErrorMessage = "Region cannot exceed 50 characters")]
    public string? Region { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}
