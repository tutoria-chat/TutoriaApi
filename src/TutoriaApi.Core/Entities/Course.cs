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

    /// <summary>
    /// Which ENEM knowledge area this course's simulados draw from when ENEM is
    /// enabled: linguagens | matematica | natureza | humanas. Null until enabled.
    /// </summary>
    public string? EnemArea { get; set; }

    // ── Lifecycle (see CourseLifecycle) ─────────────────────────────────────

    /// <summary>How long a cohort keeps the course (1–24 months). Null = no auto-removal.</summary>
    public int? DurationMonths { get; set; }

    /// <summary>When the duration was first set. The clock never starts before this,
    /// so giving an old course a duration doesn't instantly expire it.</summary>
    public DateTime? DurationSetAt { get; set; }

    /// <summary>When the current cohort's clock started (first enrollment, recorded by the
    /// daily job). Reset to null after the students are removed.</summary>
    public DateTime? LifecycleStartedAt { get; set; }

    /// <summary>Extra days granted with "hold 7 more days" (reset with the cohort).</summary>
    public int AutoRemovalExtensionDays { get; set; }

    /// <summary>Auto-removal on hold until someone resumes it.</summary>
    public bool AutoRemovalPaused { get; set; }

    /// <summary>Last time the daily job removed this course's students.</summary>
    public DateTime? StudentsRemovedAt { get; set; }

    /// <summary>"Course not in use": stopped (no alerts, no widget access) but listed, greyed out.</summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>Soft delete: hidden from lists. Only possible once deactivated.</summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public University University { get; set; } = null!;
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
    public ICollection<ProfessorCourse> ProfessorCourses { get; set; } = new List<ProfessorCourse>();
    public ICollection<CourseMajor> CourseMajors { get; set; } = new List<CourseMajor>();
}
