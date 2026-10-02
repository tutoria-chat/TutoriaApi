using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityApiKeyRepository : IRepository<UniversityApiKey>
{
    /// <summary>The non-revoked key with this hash, or null.</summary>
    Task<UniversityApiKey?> GetActiveByHashAsync(string keyHash);

    /// <summary>All keys of a university (active and revoked), newest first.</summary>
    Task<IEnumerable<UniversityApiKey>> GetByUniversityAsync(int universityId);
}
