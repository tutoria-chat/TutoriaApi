using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ILtiToolKeyRepository : IRepository<LtiToolKey>
{
    /// <summary>The key currently used for signing, or null when none exists yet.</summary>
    Task<LtiToolKey?> GetActiveAsync();

    /// <summary>
    /// Every key that should still be published in the tool JWKS — the active key
    /// plus recently retired ones, so signatures already in flight stay verifiable.
    /// </summary>
    Task<IEnumerable<LtiToolKey>> GetPublishableAsync();

    Task<LtiToolKey?> GetByKidAsync(string kid);
}
