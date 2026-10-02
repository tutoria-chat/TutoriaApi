namespace TutoriaApi.Core.Entities;

/// <summary>
/// An institution's own key for the external API (today: the Moodle grading
/// assistant plugin, quiz_tutoria). Each key works ONLY for its university — the
/// <c>universityId</c> in the request path must match — unlike the legacy shared
/// <c>ExternalApi:ApiKey</c>, which works for any institution.
///
/// SECURITY MODEL ("view once", like GitHub tokens): the external API exposes
/// student names, e-mails and essay answers, so unlike module access tokens the
/// key is NOT stored: only its SHA-256 hash (keys are 256-bit random, so an
/// unsalted hash is safe and allows lookup by hash). The plaintext is shown once,
/// at creation; afterwards only <see cref="KeyPrefix"/> identifies it. Lost keys
/// are revoked and replaced, never recovered.
/// </summary>
public class UniversityApiKey : BaseEntity
{
    public int UniversityId { get; set; }

    /// <summary>Label chosen by the admin, e.g. "Moodle produção".</summary>
    public required string Name { get; set; }

    /// <summary>Lowercase hex SHA-256 of the full key.</summary>
    public required string KeyHash { get; set; }

    /// <summary>First characters of the key (e.g. "tgk_3f9a1c"), safe to display.</summary>
    public required string KeyPrefix { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime? LastUsedAt { get; set; }

    /// <summary>Set when revoked; a revoked key never authenticates again.</summary>
    public DateTime? RevokedAt { get; set; }

    public University? University { get; set; }
}
