using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ModuleAccessTokenRepository : Repository<ModuleAccessToken>, IModuleAccessTokenRepository
{
    public ModuleAccessTokenRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<ModuleAccessToken?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ModuleAccessToken?> GetByTokenAsync(string token)
    {
        return await _dbSet
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task<(IEnumerable<ModuleAccessToken> Items, int Total)> SearchAsync(
        int? moduleId,
        int? universityId,
        bool? isActive,
        int page,
        int pageSize,
        List<int>? allowedModuleIds = null)
    {
        var query = _dbSet
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

        // Access control filter
        if (allowedModuleIds != null && allowedModuleIds.Any())
        {
            query = query.Where(t => allowedModuleIds.Contains(t.ModuleId));
        }

        // Apply filters
        if (universityId.HasValue)
        {
            query = query.Where(t => t.Module.Course.UniversityId == universityId.Value);
        }

        if (moduleId.HasValue)
        {
            query = query.Where(t => t.ModuleId == moduleId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<ModuleAccessToken>> GetByModuleIdAsync(int moduleId)
    {
        return await _dbSet
            .Where(t => t.ModuleId == moduleId)
            .ToListAsync();
    }

    public async Task<bool> ExistsByTokenAsync(string token)
    {
        return await _dbSet.AnyAsync(t => t.Token == token);
    }
}
