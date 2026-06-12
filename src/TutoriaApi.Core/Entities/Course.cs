namespace TutoriaApi.Core.Entities;

public class Course : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public int UniversityId { get; set; }
    public int? ExternalCourseId { get; set; }

    /// <summary>
    /// CSV of discipline tracks this course counts toward for academic titles
    /// (e.g. "math,programming"). Null/empty falls back to auto-detection from
    /// the course name. Tracks: math|programming|science|health|business|language|humanities.
    /// </summary>
    public string? TitleTracks { get; set; }

    /// <summary>
    /// Whether the ENEM/Vestibular mock-exam tab is offered to this course's
    /// students in the widget. Default false — only pre-vestibular courses turn
    /// it on, so university students don't see an irrelevant tab.
    /// </summary>
    public bool EnableEnem { get; set; }

    // Navigation properties
    public University University { get; set; } = null!;
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
    public ICollection<ProfessorCourse> ProfessorCourses { get; set; } = new List<ProfessorCourse>();
}
