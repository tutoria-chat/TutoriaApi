using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Per-institution keys for the external API (the Moodle grading assistant).
/// Management methods enforce that the caller administers the institution
/// (super admin, or a manager of that same university).
/// </summary>
public interface IUniversityApiKeyService
{
    /// <summary>What the dashboard shows on the plugin setup page.</summary>
    Task<UniversityApiKeySetupInfo> GetSetupInfoAsync(int universityId, User currentUser);

    /// <summary>The institution's keys (active and revoked), newest first.</summary>
    Task<IReadOnlyList<UniversityApiKey>> ListAsync(int universityId, User currentUser);

    /// <summary>Creates a key. The plaintext is returned ONLY here — it isn't stored.</summary>
    Task<CreatedUniversityApiKey> CreateAsync(int universityId, string name, User currentUser);

    /// <summary>Revokes a key of this institution; it stops working immediately.</summary>
    Task RevokeAsync(int universityId, int keyId, User currentUser);

    /// <summary>
    /// External API authentication: the university an active key belongs to, or
    /// null when the key is unknown or revoked. Records when it was last used.
    /// </summary>
    Task<int?> ValidateAsync(string? providedKey);
}

public class UniversityApiKeySetupInfo
{
    public int UniversityId { get; set; }
    public string UniversityName { get; set; } = string.Empty;

    /// <summary>Whether the grading feature is enabled for the institution.</summary>
    public bool Enabled { get; set; }
}

public class CreatedUniversityApiKey
{
    public required UniversityApiKey Key { get; init; }

    /// <summary>The full key — shown to the admin once and never again.</summary>
    public required string PlainTextKey { get; init; }
}
