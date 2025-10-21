namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for many-to-many relationship between Professors (Users) and Courses
/// Note: Professor navigation removed - relationship configured from User side only
/// </summary>
public class ProfessorCourse
{
    public int ProfessorId { get; set; }
    public int CourseId { get; set; }
}
