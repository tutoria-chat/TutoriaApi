using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class SemesterRepository : Repository<Semester>, ISemesterRepository
{
    public SemesterRepository(TutoriaDbContext context) : base(context) { }

    public async Task<List<Semester>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Where(s => s.UniversityId == universityId)
            .OrderByDescending(s => s.StartsAtUtc)
            .ToListAsync();
    }

    public async Task<Semester?> GetByIdWithUniversityAsync(int id)
    {
        return await _dbSet.Include(s => s.University).FirstOrDefaultAsync(s => s.Id == id);
    }
}
