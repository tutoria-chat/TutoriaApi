namespace TutoriaApi.Core.Entities;

/// <summary>
/// An LTI 1.3 platform registration: the trust relationship between one LMS
/// (Moodle, Canvas, Blackboard, ...) and Tutoria, scoped to a single university.
///
/// ARCHITECTURE: This is what supersedes the manual <see cref="ModuleAccessToken"/>
/// distribution flow. Instead of a professor copying a 64-char token into the LMS,
/// the institution registers Tutoria once as an LTI tool and every launch arrives
/// with a platform-signed JWT carrying the user, role and course context.
///
/// The values here are supplied by the LMS administrator when they register Tutoria
/// (Moodle: Site administration > Plugins > Activity modules > External tool > Manage tools).
/// </summary>
public class LtiRegistration : BaseEntity
{
    /// <summary>
    /// The platform's issuer identifier (the `iss` claim). For Moodle this is the
    /// site URL, e.g. "https://moodle.universidade.edu.br".
    /// Unique together with <see cref="ClientId"/>.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// The OAuth2 client_id the platform assigned to Tutoria. Arrives as the `aud`
    /// claim on every launch and must match exactly.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// The platform's OIDC authorization endpoint. We redirect the browser here
    /// during the third-party-initiated login handshake.
    /// </summary>
    public required string AuthLoginUrl { get; set; }

    /// <summary>
    /// The platform's OAuth2 token endpoint, used to obtain access tokens for
    /// calling AGS (grades) and NRPS (roster) back into the LMS.
    /// </summary>
    public required string AuthTokenUrl { get; set; }

    /// <summary>
    /// The platform's public JWKS URL. Used to fetch the key that signed the
    /// launch id_token so we can verify it.
    /// </summary>
    public required string KeySetUrl { get; set; }

    /// <summary>
    /// Human-readable label for the admin UI, e.g. "UniFor - Moodle Produção".
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The Tutoria university this platform belongs to. Every launch coming
    /// through this registration is authorised against this tenant.
    /// </summary>
    public int UniversityId { get; set; }

    /// <summary>
    /// Disables the registration without deleting it. Launches are rejected when false.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public University University { get; set; } = null!;
    public ICollection<LtiDeployment> Deployments { get; set; } = new List<LtiDeployment>();
    public ICollection<LtiContextMapping> ContextMappings { get; set; } = new List<LtiContextMapping>();
}
