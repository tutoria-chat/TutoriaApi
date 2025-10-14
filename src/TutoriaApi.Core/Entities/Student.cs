namespace TutoriaApi.Core.Entities;

public class Student : BaseEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string HashedPassword { get; set; }
    public bool IsActive { get; set; } = true;
    public int CourseId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
}
