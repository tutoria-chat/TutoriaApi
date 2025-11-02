using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class CourseDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public int ModulesCount { get; set; }
    public int ProfessorsCount { get; set; }
    public int StudentsCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CourseCreateRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Code is required")]
    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "University ID is required")]
    public int UniversityId { get; set; }
}

public class CourseUpdateRequest
{
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string? Code { get; set; }

    public string? Description { get; set; }
}

public class CourseWithDetailsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public UniversityDto? University { get; set; }
    public List<ModuleDto> Modules { get; set; } = new();
    public List<StudentDto> Students { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ModuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Semester { get; set; }
    public int? Year { get; set; }
    public int FilesCount { get; set; }
    public int TokensCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class StudentDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
