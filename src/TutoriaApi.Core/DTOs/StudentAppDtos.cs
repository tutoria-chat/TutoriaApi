namespace TutoriaApi.Core.DTOs;

// TutorIA Estudantes (B2C app) — shapes returned to the mobile app.

public record StudentPlanResponse(
    string Id, string Name, int PriceCents, int AreaCount, int EssaysPerWeek, int DailyMessages,
    bool Available, string[] Highlights);

public record StudentGuardianResponse(bool Required, string Status, string? Email, string? Name);

public class StudentMeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public int Age { get; set; }
    public string Track { get; set; } = "enem";
    public string? GoalCourse { get; set; }
    public int? EnemYear { get; set; }
    public string? University { get; set; }
    public string? Major { get; set; }
    public int? Semester { get; set; }
    public StudentPlanResponse? Plan { get; set; }
    public string? PlanSource { get; set; }
    public DateTime? PlanPeriodEnd { get; set; }
    public bool? PlanWillRenew { get; set; }
    public bool PlanBillingIssue { get; set; }
    /// <summary>ENEM areas unlocked right now.</summary>
    public List<string> Areas { get; set; } = new();
    public List<string> SelectedAreas { get; set; } = new();
    /// <summary>ENEM area → module id (the module the widget endpoints are called with).</summary>
    public Dictionary<string, int> AreaModules { get; set; } = new();
    public bool NeedsAreaChoice { get; set; }
    public bool NeedsDisciplines { get; set; }
    public bool CanChangeAreas { get; set; }
    public StudentGuardianResponse Guardian { get; set; } = new(false, "none", null, null);
    /// <summary>Guardian ok + active plan + setup done → the app can open.</summary>
    public bool Ready { get; set; }
    public bool Blocked { get; set; }
    public int MessagesToday { get; set; }
    public int Xp { get; set; }
    public bool NotifyStreak { get; set; }
    public bool NotifyUpdates { get; set; }
    public bool AgeSignalChecked { get; set; }
}

public class StudentAgentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = "spark";
    /// <summary>ENEM area id, or "disciplina".</summary>
    public string Area { get; set; } = string.Empty;
    public int ModuleId { get; set; }
    public int? DisciplineId { get; set; }
    public string? DisciplineName { get; set; }
    public string Tone { get; set; } = "amigavel";
    public string Instructions { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool Locked { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>A discipline = a Module in the student's personal course.</summary>
public class StudentDisciplineResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Professor { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int DocumentsReady { get; set; }
    public int DocumentsProcessing { get; set; }
    public int? AgentId { get; set; }
}

public class StudentFileResponse
{
    public int Id { get; set; }
    public int DisciplineId { get; set; }
    public string Filename { get; set; } = string.Empty;
    /// <summary>pdf | pptx | docx | image | text</summary>
    public string Kind { get; set; } = "pdf";
    public long SizeBytes { get; set; }
    /// <summary>processing | ready | failed</summary>
    public string Status { get; set; } = "processing";
    public DateTime? CreatedAt { get; set; }
}

public record StudentRegisterInput(string Name, string Email, string Password, DateOnly BirthDate, bool AcceptTerms);

public record StudentProfileUpdateInput(
    string? Name, string? GoalCourse, int? EnemYear, string? Track, string? University, string? Major, int? Semester,
    bool? NotifyStreak, bool? NotifyUpdates,
    // Which nullable fields were explicitly sent (so null can clear them)
    bool HasGoalCourse = false, bool HasEnemYear = false, bool HasUniversity = false, bool HasMajor = false,
    bool HasSemester = false);

public record StudentAgentInput(string? Name, string? Avatar, string? Area, int? DisciplineId, string? Tone, string? Instructions);

public record GuardianRequestInfo(string StudentName, string? GuardianName, string? DpoEmail);
