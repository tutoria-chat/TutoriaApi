using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ModuleRepository : Repository<Module>, IModuleRepository
{
    public ModuleRepository(TutoriaDbContext context) : base(context)
    {
    }

    // Override base GetByIdAsync so soft-deleted modules are never returned by any caller
    public override async Task<Module?> GetByIdAsync(int id)
    {
        return await _dbSet.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
    }

    public async Task<Module?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(m => m.Course)
                .ThenInclude(c => c.University)
            .Include(m => m.AIModel)
            .Include(m => m.Files)
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
    }

    public async Task<IEnumerable<Module>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Where(m => m.CourseId == courseId && m.IsActive)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Module>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(m => m.Course)
            .Where(m => m.Course.UniversityId == universityId && m.IsActive)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Module> Items, int Total)> SearchAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize,
        int? universityId = null,
        List<int>? allowedCourseIds = null)
    {
        var query = _dbSet
            .Include(m => m.Course)
            .Include(m => m.AIModel)
            .Where(m => m.IsActive)
            .AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(m => m.Course.UniversityId == universityId.Value);
        }

        // An explicit empty list MUST filter to zero rows; null means "no restriction".
        if (allowedCourseIds != null)
        {
            query = query.Where(m => allowedCourseIds.Contains(m.CourseId));
        }

        if (courseId.HasValue)
        {
            query = query.Where(m => m.CourseId == courseId.Value);
        }

        if (semester.HasValue)
        {
            query = query.Where(m => m.Semester == semester.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Name.Contains(search) || m.Code.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsByCodeAndCourseAsync(string code, int courseId)
    {
        // Only active modules block code reuse — soft-deleted module codes can be recycled
        return await _dbSet.AnyAsync(m => m.Code == code && m.CourseId == courseId && m.IsActive);
    }

    public async Task<Dictionary<int, int>> GetFileCountsAsync(IEnumerable<int> moduleIds)
    {
        return await _context.Files
            .Where(f => f.ModuleId.HasValue && moduleIds.Contains(f.ModuleId.Value))
            .GroupBy(f => f.ModuleId!.Value)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count);
    }

    public async Task<Dictionary<int, int>> GetTokenCountsAsync(IEnumerable<int> moduleIds)
    {
        return await _context.ModuleAccessTokens
            .Where(t => moduleIds.Contains(t.ModuleId))
            .GroupBy(t => t.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count);
    }
}
