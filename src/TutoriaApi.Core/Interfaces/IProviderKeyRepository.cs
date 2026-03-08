using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProviderKeyRepository : IRepository<ProviderKey>
{
    Task<IEnumerable<ProviderKey>> GetByProviderAsync(string provider);
    Task<IEnumerable<ProviderKey>> GetActiveKeysAsync();
    Task<IEnumerable<ProviderKey>> GetActiveKeysByProviderAsync(string provider);
    Task<ProviderKey?> GetNextAvailableKeyAsync(string provider);
    Task<bool> ExistsByKeyNameAsync(string keyName);
}
