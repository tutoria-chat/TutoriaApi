using System.ComponentModel.DataAnnotations;
using TutoriaApi.Core.Attributes;

namespace TutoriaApi.Web.Management.DTOs;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty; // student, professor, super_admin
    public bool IsActive { get; set; }
    public bool? IsAdmin { get; set; } // For professors and super_admins
    public int? UniversityId { get; set; } // For professors
    public string? UniversityName { get; set; }
    public int? CourseId { get; set; } // For students
    public string? CourseName { get; set; }
    public string ThemePreference { get; set; } = "system";
    public string LanguagePreference { get; set; } = "pt-br";
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UserCreateRequest
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

    [Required(ErrorMessage = "Password is required")]
    [PasswordComplexity(minLength: 8)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "User type is required")]
    [RegularExpression("^(student|professor|super_admin)$", ErrorMessage = "User type must be: student, professor, or super_admin")]
    public string UserType { get; set; } = string.Empty;

    // For professors only
    public int? UniversityId { get; set; }

    // For professors and super_admins
    public bool IsAdmin { get; set; } = false;

    // For students only
    public int? CourseId { get; set; }

    [MaxLength(20)]
    public string? ThemePreference { get; set; }

    [MaxLength(10)]
    public string? LanguagePreference { get; set; }
}

public class UserUpdateRequest
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

    public bool? IsAdmin { get; set; }

    public bool? IsActive { get; set; }

    public int? UniversityId { get; set; }

    public int? CourseId { get; set; }

    [MaxLength(20)]
    public string? ThemePreference { get; set; }

    [MaxLength(10)]
    public string? LanguagePreference { get; set; }
}

public class ChangeUserPasswordRequest
{
    [Required(ErrorMessage = "New password is required")]
    [PasswordComplexity(minLength: 8)]
    public string NewPassword { get; set; } = string.Empty;
}
