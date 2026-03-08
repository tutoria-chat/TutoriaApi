using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProviderKeyService
{
    Task<IEnumerable<ProviderKey>> GetAllAsync();
    Task<ProviderKey?> GetByIdAsync(int id);
    Task<IEnumerable<ProviderKey>> GetByProviderAsync(string provider);
    Task<IEnumerable<ProviderKey>> GetActiveKeysAsync();
    Task<ProviderKey> CreateAsync(ProviderKey providerKey, string rawApiKey);
    Task<ProviderKey> UpdateAsync(int id, ProviderKey providerKey, string? rawApiKey = null);
    Task DeleteAsync(int id);
    Task<string> MaskApiKey(string encryptedApiKey);
}
