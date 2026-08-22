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
        string? search = null,
        List<int>? allowedModuleIds = null)
    {
        var query = _dbSet
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

        // Access control filter. An empty list MUST still apply so a user with no
        // allowed modules sees zero tokens rather than every token in the system.
        if (allowedModuleIds != null)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ILIKE rather than Contains: on PostgreSQL, Contains translates to a
            // case-sensitive LIKE, so "MOD1" would not match a key named "mod1".
            // Wildcards in the term are escaped so a literal % or _ is searched for
            // rather than acting as a pattern.
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";

            query = query.Where(t =>
                EF.Functions.ILike(t.Name, pattern) ||
                (t.Description != null && EF.Functions.ILike(t.Description, pattern)) ||
                EF.Functions.ILike(t.Module.Name, pattern));
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

    /// <summary>
    /// Escapes LIKE/ILIKE wildcards so a search term is matched literally.
    /// Without this, a term containing % or _ silently becomes a pattern.
    /// </summary>
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

}
