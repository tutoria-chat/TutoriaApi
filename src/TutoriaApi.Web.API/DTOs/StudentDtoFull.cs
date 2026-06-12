using System.ComponentModel.DataAnnotations;
using TutoriaApi.Core.Attributes;

namespace TutoriaApi.Web.API.DTOs;

public class StudentDetailDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ExternalId { get; set; } // Matricula
    public bool IsActive { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public List<StudentCourseDto> EnrolledCourses { get; set; } = new();
    public EquippedTitleDto? EquippedTitle { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// The academic title a student chose to display, resolved from
/// StudentProgress.DisplayedTitleKey. Structured (not a label) so the UI can
/// localize it. Null when the student hasn't equipped a title.
/// </summary>
public class EquippedTitleDto
{
    public string Key { get; set; } = string.Empty;
    /// <summary>track | global | hidden</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>For track titles: math|programming|science|health|business|language|humanities</summary>
    public string? Track { get; set; }
    /// <summary>For track titles: aprendiz | mestre | lenda</summary>
    public string? Tier { get; set; }
}

public class StudentCourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

public class StudentCreateRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(100, ErrorMessage = "Username cannot exceed 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    // No password field - students don't have passwords (they don't login)

    [Required(ErrorMessage = "Matricula is required")]
    [MaxLength(100, ErrorMessage = "Matricula cannot exceed 100 characters")]
    public string ExternalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Course ID is required")]
    public int CourseId { get; set; }
}

public class StudentUpdateRequest
{
    [MaxLength(100, ErrorMessage = "Username cannot exceed 100 characters")]
    public string? Username { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? Email { get; set; }

    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string? FirstName { get; set; }

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string? LastName { get; set; }

    public bool? IsActive { get; set; }
    public int? CourseId { get; set; }
}
