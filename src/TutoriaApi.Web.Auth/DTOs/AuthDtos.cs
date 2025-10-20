using System.ComponentModel.DataAnnotations;
using TutoriaApi.Core.Attributes;

namespace TutoriaApi.Web.Auth.DTOs;

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
    public string ThemePreference { get; set; } = "system";
    public string LanguagePreference { get; set; } = "pt-br";
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
