using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CourseRepository : Repository<Course>, ICourseRepository
{
    public CourseRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<Course?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.University)
            .Include(c => c.Modules)
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Course>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Where(c => c.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Course> Items, int Total)> SearchAsync(
        int? universityId,
        string? search,
        int page,
        int pageSize)
    {
        var query = _dbSet.AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(c => c.UniversityId == universityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsByCodeAndUniversityAsync(string code, int universityId)
    {
        return await _dbSet.AnyAsync(c => c.Code == code && c.UniversityId == universityId);
    }
}
