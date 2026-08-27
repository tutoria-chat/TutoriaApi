namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for the many-to-many relationship between Courses and Majors.
/// Raw POCO — no navigation properties, to avoid EF Core issues (mirrors StudentCourse).
/// </summary>
public class CourseMajor
{
    public int CourseId { get; set; }
    public int MajorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
