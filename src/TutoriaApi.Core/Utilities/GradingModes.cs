using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Utilities;

/// <summary>
/// Which kind of help the AI grading assistant gives an institution.
///
///   feedback (default) — formative feedback drafts only, NEVER a grade. Required
///             for every institution under MEC/CNE (universities, schools): the CNE
///             Guidelines for AI in Education forbid AI-suggested grades on essays,
///             even as a draft for the teacher.
///   score   — the AI also suggests a grade from the rubric, with evidence and a
///             confidence level; a person confirms it. Only for institutions OUTSIDE
///             the MEC system (corporate training), only switched on by a Tutoria
///             super_admin, only for jobs sent with the institution's own API key.
///
/// The rule "score needs a corporate institution" is also a database check
/// constraint on Universities, so no code path can store the forbidden combination.
/// The worker re-checks the university before scoring (defense in depth).
/// </summary>
public static class GradingModes
{
    public const string Feedback = "feedback";
    public const string Score = "score";

    public const string InstitutionUniversity = "university";
    public const string InstitutionCorporate = "corporate";

    public static readonly IReadOnlyList<string> AllModes = new[] { Feedback, Score };
    public static readonly IReadOnlyList<string> AllInstitutionTypes = new[] { InstitutionUniversity, InstitutionCorporate };

    public static bool IsValidMode(string? mode) => mode != null && AllModes.Contains(mode);
    public static bool IsValidInstitutionType(string? type) => type != null && AllInstitutionTypes.Contains(type);

    /// <summary>Whether this combination may be stored at all.</summary>
    public static bool IsAllowedCombination(string institutionType, string gradingMode)
        => gradingMode == Feedback || institutionType == InstitutionCorporate;

    /// <summary>
    /// The mode a grading job actually runs with. Score only when the university is
    /// corporate, has score mode on, and the caller is trusted for it: a signed-in
    /// dashboard user or the institution's own key — never the legacy shared key,
    /// which works for every institution and has leaked before.
    /// </summary>
    public static string Effective(University? university, bool trustedCaller)
    {
        if (university == null || !trustedCaller) return Feedback;
        if (university.InstitutionType != InstitutionCorporate) return Feedback;
        return university.GradingMode == Score ? Score : Feedback;
    }
}
