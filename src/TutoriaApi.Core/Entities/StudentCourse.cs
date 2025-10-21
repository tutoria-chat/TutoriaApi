namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for many-to-many relationship between Students (Users) and Courses
/// Raw POCO - no navigation properties to avoid EF Core issues
/// </summary>
public class StudentCourse
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
