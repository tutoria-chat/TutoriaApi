using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Core.Entities;

/// <summary>
/// TutorIA Estudantes (B2C app): everything about an independent student that
/// the institutional product doesn't have — study goal, chosen ENEM areas,
/// university-track details, guardian consent (minors), the store age signal and
/// notification preferences. One row per student user (PK = UserId).
/// </summary>
public class StudentProfile : IAuditable
{
    public int UserId { get; set; }

    /// <summary>enem | university</summary>
    public string Track { get; set; } = "enem";
    public string? GoalCourse { get; set; }
    public int? EnemYear { get; set; }

    // University track
    public string? UniversityName { get; set; }
    public string? Major { get; set; }
    public int? Semester { get; set; }
    /// <summary>The student's personal Course (in the consumer university) whose Modules are their disciplines.</summary>
    public int? PersonalCourseId { get; set; }

    /// <summary>Comma-separated ENEM area ids unlocked by the Simples plan (2 of 4).</summary>
    public string? SelectedAreas { get; set; }
    /// <summary>Billing period in which the student last switched areas (one switch per cycle).</summary>
    public DateTime? AreasChangedPeriodStart { get; set; }

    // Guardian consent (required while under 18 — LGPD Art. 14, ECA Digital)
    /// <summary>none | pending | approved | denied</summary>
    public string GuardianStatus { get; set; } = "none";
    public string? GuardianName { get; set; }
    public string? GuardianEmail { get; set; }
    public string? GuardianTokenHash { get; set; }
    public DateTime? GuardianRequestedAt { get; set; }
    public DateTime? GuardianDecidedAt { get; set; }

    // Store age signal (Apple Declared Age Range / Google Play Age Signals). Only ever adds protection.
    public int? AgeSignalLower { get; set; }
    public int? AgeSignalUpper { get; set; }
    public string? AgeSignalSource { get; set; }
    public DateTime? AgeSignalAt { get; set; }

    public bool NotifyStreak { get; set; } = true;
    public bool NotifyUpdates { get; set; } = true;
    public DateOnly? LastStreakReminder { get; set; }

    public DateTime? TermsAcceptedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
