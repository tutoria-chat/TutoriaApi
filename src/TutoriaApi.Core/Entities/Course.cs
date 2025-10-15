namespace TutoriaApi.Core.Entities;

public class Course : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public int UniversityId { get; set; }

    // Navigation properties
    public University University { get; set; } = null!;
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<ProfessorCourse> ProfessorCourses { get; set; } = new List<ProfessorCourse>();
}
