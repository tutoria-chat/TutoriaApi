using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IConsentRepository : IRepository<Consent>
{
    Task<IEnumerable<Consent>> GetByUserIdAsync(int userId);
    Task<Consent?> GetActiveConsentAsync(int userId, string consentType);
    Task<bool> HasActiveConsentAsync(int userId, string consentType);
    Task<IEnumerable<Consent>> GetAllActiveConsentsAsync(int userId);
}
