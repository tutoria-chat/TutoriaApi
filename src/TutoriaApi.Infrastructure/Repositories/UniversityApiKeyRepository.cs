using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UniversityApiKeyRepository : Repository<UniversityApiKey>, IUniversityApiKeyRepository
{
    public UniversityApiKeyRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<UniversityApiKey?> GetActiveByHashAsync(string keyHash)
    {
        return await _dbSet.FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.RevokedAt == null);
    }

    public async Task<IEnumerable<UniversityApiKey>> GetByUniversityAsync(int universityId)
    {
        return await _dbSet
            .Where(k => k.UniversityId == universityId)
            .OrderByDescending(k => k.CreatedAt)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }
}
