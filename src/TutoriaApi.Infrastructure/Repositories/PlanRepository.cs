using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class PlanRepository : Repository<Plan>, IPlanRepository
{
    public PlanRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Plan>> GetActivePlansAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Plan?> GetBySlugAsync(string slug)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<Plan?> GetWithSubscriptionsAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Subscriptions)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> ExistsBySlugAsync(string slug)
    {
        return await _dbSet.AnyAsync(p => p.Slug == slug);
    }
}
