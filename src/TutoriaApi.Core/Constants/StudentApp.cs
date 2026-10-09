using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Constants;

/// <summary>
/// TutorIA Estudantes (B2C app) catalog and entitlement rules.
/// The Python API (tutoria-api app/services/student_app/plans.py) mirrors the
/// plan ids, limits and area codes — keep both in sync.
/// </summary>
public static class StudentApp
{
    /// <summary>University.Code of the consumer tenant (seeded at startup).</summary>
    public const string ConsumerUniversityCode = "TUTORIA-ESTUDANTES";
    public const string EnemCourseCode = "ENEM";

    public static readonly string[] AreaIds = { "linguagens", "humanas", "natureza", "matematica" };

    /// <summary>ENEM area → Module.Code of its module in the consumer ENEM course.</summary>
    public static readonly IReadOnlyDictionary<string, string> AreaModuleCodes = new Dictionary<string, string>
    {
        ["linguagens"] = "ENEM-LINGUAGENS",
        ["humanas"] = "ENEM-HUMANAS",
        ["natureza"] = "ENEM-NATUREZA",
        ["matematica"] = "ENEM-MATEMATICA",
    };

    public static readonly IReadOnlyDictionary<string, string> AreaLabels = new Dictionary<string, string>
    {
        ["linguagens"] = "Linguagens",
        ["humanas"] = "Humanas",
        ["natureza"] = "Natureza",
        ["matematica"] = "Matemática",
    };

    public static readonly string[] Tones = { "amigavel", "direto", "socratico", "divertido" };

    public const int MinAge = 13;
    public const int AdultAge = 18;
    public const int MaxAgents = 12;
    public const int MaxDisciplines = 15;
    public const int MaxDocuments = 150;
    public const int MaxUploadMb = 20;
    public const int DailyUploads = 20;
    public const int GuardianLinkDays = 7;

    public sealed record PlanInfo(
        string Id, string Name, int PriceCents, int AreaCount, int EssaysPerWeek, int DailyMessages,
        bool Available, string[] Highlights);

    public static readonly IReadOnlyList<PlanInfo> Plans = new[]
    {
        new PlanInfo("simples", "Simples", 1990, 2, 0, 60, true,
            new[] { "2 áreas do ENEM à sua escolha", "Agentes de estudo personalizados", "Questões oficiais e flashcards" }),
        new PlanInfo("pro", "Pro", 2990, 4, 0, 120, true,
            new[] { "As 4 áreas do ENEM", "Agentes de estudo personalizados", "Questões oficiais e flashcards" }),
        new PlanInfo("max", "Max", 4490, 4, 2, 200, true,
            new[] { "As 4 áreas do ENEM", "2 correções de redação por semana", "Tudo do Pro" }),
        new PlanInfo("universitario", "Universitário", 4490, 0, 0, 200, true,
            new[] { "Um agente para cada disciplina do seu curso", "Envie PDFs, slides e fotos da sua faculdade",
                    "Respostas com base no seu material" }),
    };

    /// <summary>Highest first: a customer holding two store entitlements gets the better one.</summary>
    public static readonly string[] PlanRank = { "max", "universitario", "pro", "simples" };

    public static PlanInfo? FindPlan(string? id) => Plans.FirstOrDefault(p => p.Id == id);

    // ─── Rules (pure; unit-tested) ──────────────────────────────────────────

    public static int AgeOn(DateTime birth, DateOnly today)
    {
        var age = today.Year - birth.Year;
        if (today.Month < birth.Month || (today.Month == birth.Month && today.Day < birth.Day)) age--;
        return age;
    }

    /// <summary>Typed birth date OR the store's age signal — whichever is more protective.</summary>
    public static bool IsMinor(DateTime? birth, StudentProfile? profile, DateOnly today)
    {
        var typedMinor = birth == null || AgeOn(birth.Value, today) < AdultAge;
        var signalMinor = profile?.AgeSignalUpper is int upper && upper < AdultAge;
        return typedMinor || signalMinor;
    }

    public static bool BelowMinAge(StudentProfile? profile)
        => profile?.AgeSignalUpper is int upper && upper < MinAge;

    public static bool GuardianOk(DateTime? birth, StudentProfile? profile, DateOnly today)
        => !IsMinor(birth, profile, today) || profile?.GuardianStatus == "approved";

    public static PlanInfo? ActivePlan(StudentSubscription? sub, DateTime nowUtc)
    {
        if (sub == null || sub.PeriodEnd == null || sub.PeriodEnd <= nowUtc) return null;
        return FindPlan(sub.Plan);
    }

    public static List<string> SelectedAreas(StudentProfile? profile)
        => (profile?.SelectedAreas ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => AreaIds.Contains(a))
            .Distinct()
            .ToList();

    /// <summary>ENEM areas unlocked right now (in canonical order).</summary>
    public static List<string> AllowedAreas(PlanInfo? plan, StudentProfile? profile)
    {
        if (plan == null || plan.AreaCount == 0) return new List<string>();
        if (plan.AreaCount >= AreaIds.Length) return AreaIds.ToList();
        var chosen = SelectedAreas(profile);
        return AreaIds.Where(chosen.Contains).ToList();
    }

    public static bool NeedsAreaChoice(PlanInfo? plan, StudentProfile? profile)
        => plan != null && plan.AreaCount > 0 && plan.AreaCount < AreaIds.Length
           && AllowedAreas(plan, profile).Count < plan.AreaCount;

    /// <summary>First choice is free; then one switch per billing period.</summary>
    public static bool CanChangeAreas(StudentProfile? profile, StudentSubscription? sub)
        => SelectedAreas(profile).Count == 0 || profile!.AreasChangedPeriodStart != sub?.PeriodStart;

    /// <summary>Most recent Sunday 00:00 in Brasília time — when the essay quota resets (UTC).</summary>
    public static DateTime WeekStartUtc(DateTime nowUtc)
    {
        var local = nowUtc.AddHours(-3); // Brazil has no DST since 2019
        var daysSinceSunday = (int)local.DayOfWeek;
        var startLocal = local.Date.AddDays(-daysSinceSunday);
        return DateTime.SpecifyKind(startLocal.AddHours(3), DateTimeKind.Utc);
    }

    public static DateOnly TodayBrazil(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc.AddHours(-3));
}
