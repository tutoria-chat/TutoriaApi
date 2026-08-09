namespace TutoriaApi.Core.Entities;

/// <summary>
/// Tutoria's own RSA key pair, used when Tutoria acts as the LTI *tool*:
///  - signing Deep Linking responses returned to the platform;
///  - signing the client_credentials assertion used to obtain AGS / NRPS tokens.
///
/// The public half is published at the tool JWKS endpoint so platforms can verify
/// our signatures. Keys are versioned by <see cref="Kid"/> so they can be rotated:
/// publish the new key, mark it active, and keep the previous one published
/// (but inactive) until any in-flight tokens signed with it have expired.
///
/// SECURITY: <see cref="PrivateKeyPem"/> is a real secret — unlike ModuleAccessToken
/// it is never exposed through any API, and is only read server-side when signing.
/// </summary>
public class LtiToolKey : BaseEntity
{
    /// <summary>
    /// The JWK key id. Emitted in the JWT header so the platform knows which
    /// published key to verify against.
    /// </summary>
    public required string Kid { get; set; }

    /// <summary>
    /// PKCS#8 PEM-encoded RSA private key. Never leaves the server.
    /// </summary>
    public required string PrivateKeyPem { get; set; }

    /// <summary>
    /// SPKI PEM-encoded RSA public key. Converted to JWK form for the JWKS endpoint.
    /// </summary>
    public required string PublicKeyPem { get; set; }

    /// <summary>
    /// Exactly one key should be active at a time — that is the one used for signing.
    /// Inactive keys stay in the JWKS until their signatures can no longer be in flight.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional retirement date, for auditing key rotation.
    /// </summary>
    public DateTime? RetiredAt { get; set; }
}
