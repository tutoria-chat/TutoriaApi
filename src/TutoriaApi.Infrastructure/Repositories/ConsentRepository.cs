using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ConsentRepository : Repository<Consent>, IConsentRepository
{
    public ConsentRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Consent>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AcceptedAt)
            .ToListAsync();
    }

    public async Task<Consent?> GetActiveConsentAsync(int userId, string consentType)
    {
        return await _dbSet
            .Where(c => c.UserId == userId
                     && c.ConsentType == consentType
                     && c.RevokedAt == null)
            .OrderByDescending(c => c.AcceptedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasActiveConsentAsync(int userId, string consentType)
    {
        return await _dbSet
            .AnyAsync(c => c.UserId == userId
                        && c.ConsentType == consentType
                        && c.RevokedAt == null);
    }

    public async Task<IEnumerable<Consent>> GetAllActiveConsentsAsync(int userId)
    {
        return await _dbSet
            .Where(c => c.UserId == userId && c.RevokedAt == null)
            .OrderByDescending(c => c.AcceptedAt)
            .ToListAsync();
    }
}
