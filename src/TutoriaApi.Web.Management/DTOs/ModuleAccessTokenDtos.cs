using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.Management.DTOs;

public class ModuleAccessTokenListDto
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AllowChat { get; set; }
    public bool AllowFileAccess { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ModuleAccessTokenDetailDto
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public int? CreatedByProfessorId { get; set; }
    public string? CreatedByName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AllowChat { get; set; }
    public bool AllowFileAccess { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ModuleAccessTokenCreateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Module ID is required")]
    public int ModuleId { get; set; }

    public bool AllowChat { get; set; } = true;

    public bool AllowFileAccess { get; set; } = true;

    [Range(1, 3650, ErrorMessage = "Expiration days must be between 1 and 3650 (10 years)")]
    public int? ExpiresInDays { get; set; }
}

public class ModuleAccessTokenUpdateRequest
{
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public bool? AllowChat { get; set; }

    public bool? AllowFileAccess { get; set; }
}
