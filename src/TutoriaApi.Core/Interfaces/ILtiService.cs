using TutoriaApi.Core.Lti;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Tutoria acting as an LTI 1.3 Advantage <em>tool</em>: the OIDC handshake, launch
/// validation, the published key set, and Deep Linking responses.
/// </summary>
public interface ILtiService
{
    /// <summary>
    /// Step 1 of the launch — third-party-initiated login. Resolves the platform,
    /// mints a single-use nonce plus state, and returns the platform authorization
    /// URL the browser must be redirected to.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The (issuer, client_id) is not registered.</exception>
    /// <exception cref="InvalidOperationException">The registration is disabled.</exception>
    Task<string> BuildLoginRedirectAsync(LtiLoginRequest request);

    /// <summary>
    /// Step 2 — validate the id_token returned by the platform and resolve it to
    /// Tutoria entities.
    ///
    /// Verifies, in order: the state/nonce pair is known and unconsumed; the token
    /// signature against the platform's published JWKS; issuer; audience equals our
    /// client_id; expiry/not-before; LTI version; a known active deployment; and
    /// that any module referenced by custom parameters belongs to the registration's
    /// university.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Any validation step failed.</exception>
    Task<LtiLaunchResult> ValidateLaunchAsync(string idToken, string state);

    /// <summary>
    /// The tool's public key set, as the JSON object served at the JWKS endpoint.
    /// Generates an initial key pair on first call if none exists.
    /// </summary>
    Task<object> GetPublicKeySetAsync();

    /// <summary>
    /// Builds and signs the JWT returned to the platform at the end of a Deep
    /// Linking flow, embedding the chosen module as a custom parameter so the
    /// subsequent resource launches identify it.
    /// </summary>
    Task<string> BuildDeepLinkingResponseAsync(LtiLaunchResult launch, int moduleId, string? title);

    /// <summary>
    /// Mints a short-lived, single-module access token for a validated launch.
    ///
    /// The chat widget authenticates with a module access token, so rather than
    /// changing that contract an LTI launch issues a throwaway one that expires in
    /// minutes. This keeps long-lived shareable tokens out of URLs while requiring
    /// no changes to the widget or the AI API.
    /// </summary>
    Task<string> CreateEphemeralModuleTokenAsync(int moduleId, string subject);
}

/// <summary>
/// The parameters a platform sends on the initial login request. Both GET and POST
/// form encodings are permitted by the spec, so this is bound from either.
/// </summary>
public class LtiLoginRequest
{
    public required string Iss { get; set; }
    public string? ClientId { get; set; }
    public string? LoginHint { get; set; }
    public string? LtiMessageHint { get; set; }
    public string? TargetLinkUri { get; set; }
    public string? LtiDeploymentId { get; set; }
}
