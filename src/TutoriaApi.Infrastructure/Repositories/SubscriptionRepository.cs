using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<Subscription?> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.University)
            .Where(s => s.UniversityId == universityId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Subscription?> GetActiveByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.University)
            .Where(s => s.UniversityId == universityId)
            .Where(s => s.Status == "active" || s.Status == "trialing")
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Subscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.University)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);
    }

    public async Task<Subscription?> GetWithPlanAsync(int id)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.University)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Subscription>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.University)
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }
}
