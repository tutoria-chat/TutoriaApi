using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UniversityRepository : Repository<University>, IUniversityRepository
{
    public UniversityRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<University?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Name == name);
    }

    public async Task<University?> GetByCodeAsync(string code)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Code == code);
    }

    public async Task<University?> GetByIdWithCoursesAsync(int id)
    {
        return await _dbSet
            .Include(u => u.Courses)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _dbSet.AnyAsync(u => u.Name == name);
    }

    public async Task<bool> ExistsByCodeAsync(string code)
    {
        return await _dbSet.AnyAsync(u => u.Code == code);
    }

    public async Task<(IEnumerable<University> Items, int Total)> SearchAsync(string? search, int page, int pageSize)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Name.Contains(search) || u.Code.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
