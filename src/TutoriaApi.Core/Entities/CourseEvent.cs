namespace TutoriaApi.Core.Entities;

/// <summary>
/// A dated occurrence in a course's calendar: tests, assignment due dates,
/// holidays, field events. Assignment-type events link to an Assignment.
/// Times are stored in UTC; the UI captures/displays them in America/Sao_Paulo.
/// </summary>
public class CourseEvent : BaseEntity
{
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>Optional module scoping (e.g. a test for one module only).</summary>
    public int? ModuleId { get; set; }
    public Module? Module { get; set; }

    /// <summary>Set when EventType == "assignment" — the calendar entry mirrors the assignment.</summary>
    public int? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>test | assignment | holiday | field_event | other</summary>
    public required string EventType { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    // Reminder emails to enrolled students before StartsAtUtc
    public bool Remind7Days { get; set; }
    public bool Remind3Days { get; set; }
    public bool Remind2Days { get; set; }
    public bool Remind24Hours { get; set; }

    public int CreatedByUserId { get; set; }
}
