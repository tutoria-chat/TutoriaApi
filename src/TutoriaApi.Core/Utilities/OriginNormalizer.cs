namespace TutoriaApi.Core.Utilities;

/// <summary>
/// Normalizes user-entered web addresses into browser "Origin" form
/// (scheme://host[:port], no path, lowercased) so they match what the browser
/// sends in the <c>Origin</c> header. Customers may paste a full page URL or a
/// bare host; this cleans it up.
/// </summary>
public static class OriginNormalizer
{
    /// <summary>Normalize one entry, or null if it isn't a valid http(s) address.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var s = input.Trim();
        if (!s.Contains("://")) s = "https://" + s;

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        // GetLeftPart(Authority) → scheme://host[:port] (drops path/query and default ports).
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }

    /// <summary>
    /// Split a stored multi-line/comma value into distinct normalized origins.
    /// </summary>
    public static List<string> NormalizeMany(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var part in raw.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var norm = Normalize(part);
            if (norm != null && !result.Contains(norm)) result.Add(norm);
        }
        return result;
    }
}
