namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for many-to-many relationship between Professors and Courses
/// </summary>
public class ProfessorCourse
{
    public int ProfessorId { get; set; }
    public int CourseId { get; set; }

    // Navigation properties
    public Professor Professor { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
