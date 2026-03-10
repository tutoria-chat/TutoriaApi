using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TutoriaApi.Core.Attributes;

namespace TutoriaApi.Web.API.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Client ID for server-to-server authentication (e.g., Next.js backend).
    /// Optional if Authorization header with client token is provided.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for server-to-server authentication.
    /// Optional if Authorization header with client token is provided.
    /// </summary>
    public string? ClientSecret { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }

    // Minimal user info for client-side routing/permissions
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
}

public class RegisterStudentRequest
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

    public List<int> CourseIds { get; set; } = new List<int>();
}

public class PasswordResetRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;
}

public class PasswordResetDto
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [PasswordComplexity(minLength: 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public bool? IsAdmin { get; set; }
    public string? GovernmentId { get; set; }
    public string? ExternalId { get; set; }
    public DateTime? Birthdate { get; set; }
    public List<int>? StudentCourseIds { get; set; } // For students with multiple courses
    public List<int>? ProfessorCourseIds { get; set; } // For professors assigned to courses
    public DateTime? LastLoginAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string ThemePreference { get; set; } = "system";
    public string LanguagePreference { get; set; } = "pt-br";
    public List<string>? Permissions { get; set; } // Effective permissions (role + extra)
    public List<string>? ExtraPermissions { get; set; } // User-specific extra permissions only
}

public class UpdateProfileRequest
{
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string? FirstName { get; set; }

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string? LastName { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? Email { get; set; }

    [MaxLength(50, ErrorMessage = "Government ID cannot exceed 50 characters")]
    public string? GovernmentId { get; set; }

    [MaxLength(100, ErrorMessage = "External ID cannot exceed 100 characters")]
    public string? ExternalId { get; set; }

    public DateTime? Birthdate { get; set; }

    [MaxLength(20)]
    public string? ThemePreference { get; set; }

    [MaxLength(10)]
    public string? LanguagePreference { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [PasswordComplexity(minLength: 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
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
    [RegularExpression("^(student|professor|super_admin|manager|tutor|platform_coordinator)$",
        ErrorMessage = "User type must be: student, professor, super_admin, manager, tutor, or platform_coordinator")]
    public string UserType { get; set; } = string.Empty;

    public int? UniversityId { get; set; }
    public bool IsAdmin { get; set; } = false;
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

public class ActivateStudentRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Matricula is required")]
    [MaxLength(100, ErrorMessage = "Matricula cannot exceed 100 characters")]
    public string Matricula { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [PasswordComplexity(minLength: 8)]
    public string Password { get; set; } = string.Empty;
}

public class ActivateStudentResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class RegisterUniversityRequest
{
    [Required(ErrorMessage = "University name is required")]
    [MaxLength(200, ErrorMessage = "University name cannot exceed 200 characters")]
    public string UniversityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "University code is required")]
    [MaxLength(50, ErrorMessage = "University code cannot exceed 50 characters")]
    public string UniversityCode { get; set; } = string.Empty;

    public string? UniversityDescription { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Optional — auto-derived from email prefix if not provided.
    /// </summary>
    [MaxLength(100)]
    public string? AdminUsername { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [PasswordComplexity(minLength: 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Plan slug (e.g. "starter", "professional"). Resolved to PlanId internally.
    /// Also accepts PlanId for backward compatibility.
    /// </summary>
    [Required(ErrorMessage = "Plan slug is required")]
    public string PlanSlug { get; set; } = string.Empty;

    /// <summary>
    /// Deprecated — use PlanSlug instead. Kept for backward compatibility.
    /// </summary>
    public int? PlanId { get; set; }
}

public class RegisterUniversityResponse
{
    public int UniversityId { get; set; }
    public string UniversityName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
}
