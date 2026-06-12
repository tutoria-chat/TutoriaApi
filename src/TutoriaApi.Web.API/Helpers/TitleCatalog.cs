using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Helpers;

/// <summary>
/// Parses a stored title key (StudentProgress.DisplayedTitleKey) into a
/// structured <see cref="EquippedTitleDto"/> for admin/professor views. The
/// canonical catalog lives in tutoria-api (titles_service.py); this mirrors only
/// the key shapes needed to classify a key — labels are localized in the UI.
///
/// Key shapes:
///   • track titles: "{track}_{tier}"  e.g. "math_mestre"
///   • global titles: "veterano" | "imortal"
///   • hidden title:  "transcendente"
/// </summary>
public static class TitleCatalog
{
    private static readonly HashSet<string> Tracks = new()
    {
        "math", "programming", "science", "health", "business", "language", "humanities",
    };
    private static readonly HashSet<string> Tiers = new() { "aprendiz", "mestre", "lenda" };
    private static readonly HashSet<string> Globals = new() { "veterano", "imortal" };
    private const string HiddenKey = "transcendente";

    /// <summary>Resolve a stored key, or null if empty/unrecognized.</summary>
    public static EquippedTitleDto? Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (key == HiddenKey)
            return new EquippedTitleDto { Key = key, Type = "hidden" };

        if (Globals.Contains(key))
            return new EquippedTitleDto { Key = key, Type = "global" };

        var idx = key.LastIndexOf('_');
        if (idx > 0)
        {
            var track = key[..idx];
            var tier = key[(idx + 1)..];
            if (Tracks.Contains(track) && Tiers.Contains(tier))
                return new EquippedTitleDto { Key = key, Type = "track", Track = track, Tier = tier };
        }

        return null; // unknown key — render nothing rather than guess
    }
}
