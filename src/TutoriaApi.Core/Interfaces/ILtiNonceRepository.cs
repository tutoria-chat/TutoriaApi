using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ILtiNonceRepository : IRepository<LtiNonce>
{
    Task<LtiNonce?> GetByNonceAsync(string nonce);

    /// <summary>
    /// Resolves the pending handshake by the `state` value we issued at login.
    /// This is how a launch finds its registration before the id_token is trusted.
    /// </summary>
    Task<LtiNonce?> GetByStateAsync(string state);

    /// <summary>
    /// Atomically marks a nonce as used and reports whether this caller won the
    /// race. Returns false when the nonce is unknown, expired, or already consumed —
    /// which the spec requires us to treat as a replayed launch.
    /// </summary>
    Task<bool> TryConsumeAsync(string nonce, string state);

    /// <summary>Deletes handshakes past their expiry. Returns the number removed.</summary>
    Task<int> PurgeExpiredAsync();
}
