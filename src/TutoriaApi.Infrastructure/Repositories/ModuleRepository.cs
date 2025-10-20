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

    public async Task<Module?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(m => m.Course)
                .ThenInclude(c => c.University)
            .Include(m => m.Files)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Module>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Where(m => m.CourseId == courseId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Module>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(m => m.Course)
            .Where(m => m.Course.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Module> Items, int Total)> SearchAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize)
    {
        var query = _dbSet.Include(m => m.Course).AsQueryable();

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
            .OrderBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsByCodeAndCourseAsync(string code, int courseId)
    {
        return await _dbSet.AnyAsync(m => m.Code == code && m.CourseId == courseId);
    }

    public async Task<Dictionary<int, int>> GetFileCountsAsync(IEnumerable<int> moduleIds)
    {
        return await _context.Files
            .Where(f => moduleIds.Contains(f.ModuleId))
            .GroupBy(f => f.ModuleId)
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
