namespace TutoriaApi.Core.Entities;

/// <summary>
/// AI-generated weekly study plan for one student in one course.
/// Quota: ONE plan per (student, course, ISO week) — enforced by a unique index
/// on (StudentId, CourseId, WeekStart). Generated on demand by tutoria-api from
/// the student's quiz performance, course content and calendar events.
/// </summary>
public class StudyPlan : BaseEntity
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>Monday of the plan's week (date only, America/Sao_Paulo).</summary>
    public DateTime WeekStart { get; set; }

    /// <summary>light | balanced | intensive — how heavy the student wants the week.</summary>
    public string Style { get; set; } = "balanced";

    /// <summary>Free-form learning preferences the student typed (optional).</summary>
    public string? PreferencesText { get; set; }

    /// <summary>Short AI overview shown at the top of the plan.</summary>
    public string OverviewText { get; set; } = string.Empty;

    /// <summary>JSON: [{date, title, focus, tasks:[{description, duration_min, module_id?}]}]</summary>
    public string DaysJson { get; set; } = "[]";

    /// <summary>JSON array of weak concepts the plan targets (from quiz answers).</summary>
    public string? WeakConceptsJson { get; set; }

    public string Language { get; set; } = "pt-br";

    // Email delivery
    public bool PlanEmailSent { get; set; }
    public bool DailyReminderOptIn { get; set; }
}
