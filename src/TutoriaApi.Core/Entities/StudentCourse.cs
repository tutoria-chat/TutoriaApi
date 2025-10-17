namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for many-to-many relationship between Students and Courses
/// </summary>
public class StudentCourse
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
