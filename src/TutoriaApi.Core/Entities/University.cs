namespace TutoriaApi.Core.Entities;

public class University : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Professor> Professors { get; set; } = new List<Professor>();
}
