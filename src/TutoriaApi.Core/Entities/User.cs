namespace TutoriaApi.Core.Entities;

/// <summary>
/// Unified user model that consolidates Professors, SuperAdmins, and Students
/// </summary>
public class User
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? HashedPassword { get; set; } // Nullable - students don't have passwords!
    public required string UserType { get; set; } // 'professor', 'super_admin', 'student'
    public bool IsActive { get; set; } = true;

    // Professor-specific fields (nullable for non-professors)
    public int? UniversityId { get; set; }
    public bool? IsAdmin { get; set; } = false;

    // Additional profile fields
    public string? GovernmentId { get; set; } // CPF (Brazil), SSN (US), etc.
    public string? ExternalId { get; set; } // Student registration ID, employee ID, etc.
    public DateTime? Birthdate { get; set; }

    // Common fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }
    public string ThemePreference { get; set; } = "system";
    public string LanguagePreference { get; set; } = "pt-br";

    // Navigation properties
    public University? University { get; set; }
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
