namespace TutoriaApi.Core.Entities;

public class Professor : BaseEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string HashedPassword { get; set; }
    public bool IsAdmin { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int UniversityId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }
    public string ThemePreference { get; set; } = "system";
    public string LanguagePreference { get; set; } = "pt-br";

    // Navigation properties
    public University University { get; set; } = null!;
    public ICollection<ProfessorCourse> ProfessorCourses { get; set; } = new List<ProfessorCourse>();
}
