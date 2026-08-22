using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class LtiToolKeyRepository : Repository<LtiToolKey>, ILtiToolKeyRepository
{
    /// <summary>
    /// How long a retired key stays in the published JWKS. Comfortably longer than
    /// the lifetime of any token we sign, so rotation never invalidates a signature
    /// that is still in flight.
    /// </summary>
    private static readonly TimeSpan RetiredKeyGracePeriod = TimeSpan.FromDays(7);

    public LtiToolKeyRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<LtiToolKey?> GetActiveAsync()
    {
        return await _dbSet
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<LtiToolKey>> GetPublishableAsync()
    {
        var cutoff = DateTime.UtcNow - RetiredKeyGracePeriod;

        return await _dbSet
            .Where(k => k.IsActive || (k.RetiredAt != null && k.RetiredAt > cutoff))
            .OrderByDescending(k => k.IsActive)
            .ThenByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    public async Task<LtiToolKey?> GetByKidAsync(string kid)
    {
        return await _dbSet.FirstOrDefaultAsync(k => k.Kid == kid);
    }
}
