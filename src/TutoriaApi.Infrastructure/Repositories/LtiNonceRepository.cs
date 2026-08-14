using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class LtiNonceRepository : Repository<LtiNonce>, ILtiNonceRepository
{
    public LtiNonceRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<LtiNonce?> GetByNonceAsync(string nonce)
    {
        return await _dbSet.FirstOrDefaultAsync(n => n.Nonce == nonce);
    }

    public async Task<LtiNonce?> GetByStateAsync(string state)
    {
        return await _dbSet
            .Where(n => n.State == state && n.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(n => n.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> TryConsumeAsync(string nonce, string state)
    {
        var now = DateTime.UtcNow;

        // A single conditional UPDATE, so two launches replaying the same nonce
        // cannot both succeed: the database decides the winner and the affected
        // row count tells us which caller that was. Doing this as read-then-write
        // would leave a race window wide enough to accept a replayed id_token.
        var affected = await _dbSet
            .Where(n => n.Nonce == nonce
                     && n.State == state
                     && n.ConsumedAt == null
                     && n.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.ConsumedAt, now)
                .SetProperty(n => n.UpdatedAt, now));

        return affected == 1;
    }

    public async Task<int> PurgeExpiredAsync()
    {
        return await _dbSet
            .Where(n => n.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }
}
