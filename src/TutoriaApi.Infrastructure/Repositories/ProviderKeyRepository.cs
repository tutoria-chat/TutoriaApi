using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ProviderKeyRepository : Repository<ProviderKey>, IProviderKeyRepository
{
    public ProviderKeyRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ProviderKey>> GetByProviderAsync(string provider)
    {
        return await _dbSet
            .Where(pk => pk.Provider == provider)
            .OrderBy(pk => pk.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProviderKey>> GetActiveKeysAsync()
    {
        return await _dbSet
            .Where(pk => pk.IsActive)
            .OrderBy(pk => pk.Provider)
            .ThenBy(pk => pk.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProviderKey>> GetActiveKeysByProviderAsync(string provider)
    {
        return await _dbSet
            .Where(pk => pk.Provider == provider && pk.IsActive)
            .Where(pk => pk.CooldownUntil == null || pk.CooldownUntil <= DateTime.UtcNow)
            .OrderBy(pk => pk.Priority)
            .ToListAsync();
    }

    public async Task<ProviderKey?> GetNextAvailableKeyAsync(string provider)
    {
        return await _dbSet
            .Where(pk => pk.Provider == provider && pk.IsActive)
            .Where(pk => pk.CooldownUntil == null || pk.CooldownUntil <= DateTime.UtcNow)
            .OrderBy(pk => pk.Priority)
            .ThenBy(pk => pk.FailureCount)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsByKeyNameAsync(string keyName)
    {
        return await _dbSet.AnyAsync(pk => pk.KeyName == keyName);
    }
}
