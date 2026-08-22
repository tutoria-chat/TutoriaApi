namespace TutoriaApi.Core.Entities;

/// <summary>
/// A single-use nonce issued during the OIDC login handshake and echoed back in the
/// launch id_token.
///
/// SECURITY: the LTI 1.3 spec requires the tool to reject a replayed id_token. We
/// generate the nonce at login, then consume it at launch — a second launch carrying
/// the same nonce is refused. Expired rows are pruned periodically; the
/// <see cref="ExpiresAt"/> index exists for that sweep.
/// </summary>
public class LtiNonce : BaseEntity
{
    /// <summary>
    /// The random nonce value. Unique.
    /// </summary>
    public required string Nonce { get; set; }

    /// <summary>
    /// The opaque OIDC `state` we generated alongside the nonce, echoed by the
    /// platform. Bound together so a launch cannot mix state from one login with
    /// the nonce of another.
    /// </summary>
    public required string State { get; set; }

    /// <summary>
    /// The registration this handshake belongs to.
    /// </summary>
    public int LtiRegistrationId { get; set; }

    /// <summary>
    /// Where the platform asked us to land after login (the `target_link_uri`).
    /// Validated at launch to prevent open-redirect abuse.
    /// </summary>
    public string? TargetLinkUri { get; set; }

    /// <summary>
    /// Set when the nonce is consumed by a launch. A non-null value means any
    /// further launch presenting this nonce is a replay and must be rejected.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
