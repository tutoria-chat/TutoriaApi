using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class UniversityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Fantasy Name (Nome Fantasia)
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; } // CNPJ in Brazil
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Website { get; set; }
    public int SubscriptionTier { get; set; } // 1 = Basic, 2 = Standard, 3 = Premium
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UniversityCreateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Code is required")]
    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty; // Fantasy Name (Nome Fantasia)

    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string? Address { get; set; }

    [MaxLength(20, ErrorMessage = "Tax ID cannot exceed 20 characters")]
    public string? TaxId { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Contact email cannot exceed 255 characters")]
    public string? ContactEmail { get; set; }

    [MaxLength(50, ErrorMessage = "Contact phone cannot exceed 50 characters")]
    public string? ContactPhone { get; set; }

    [MaxLength(200, ErrorMessage = "Contact person cannot exceed 200 characters")]
    public string? ContactPerson { get; set; }

    [MaxLength(255, ErrorMessage = "Website cannot exceed 255 characters")]
    public string? Website { get; set; }

    [Range(1, 3, ErrorMessage = "Subscription tier must be between 1 and 3")]
    public int SubscriptionTier { get; set; } = 3;
}

public class UniversityUpdateRequest
{
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string? Code { get; set; }

    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string? Address { get; set; }

    [MaxLength(20, ErrorMessage = "Tax ID cannot exceed 20 characters")]
    public string? TaxId { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Contact email cannot exceed 255 characters")]
    public string? ContactEmail { get; set; }

    [MaxLength(50, ErrorMessage = "Contact phone cannot exceed 50 characters")]
    public string? ContactPhone { get; set; }

    [MaxLength(200, ErrorMessage = "Contact person cannot exceed 200 characters")]
    public string? ContactPerson { get; set; }

    [MaxLength(255, ErrorMessage = "Website cannot exceed 255 characters")]
    public string? Website { get; set; }

    [Range(1, 3, ErrorMessage = "Subscription tier must be between 1 and 3")]
    public int? SubscriptionTier { get; set; }
}

public class UniversityWithCoursesDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Fantasy Name (Nome Fantasia)
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Website { get; set; }
    public int SubscriptionTier { get; set; } // 1 = Basic, 2 = Standard, 3 = Premium
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ProfessorsCount { get; set; }
    public int CoursesCount { get; set; }
    public List<CourseDetailDto> Courses { get; set; } = new();
}

public class CourseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public int Pages { get; set; }
}
