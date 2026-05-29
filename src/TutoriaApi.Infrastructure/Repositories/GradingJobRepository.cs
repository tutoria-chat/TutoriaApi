using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class GradingJobRepository : Repository<GradingJob>, IGradingJobRepository
{
    public GradingJobRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<GradingJob?> GetByIdWithCourseAsync(int id)
    {
        return await _dbSet
            .Include(j => j.Course)
            .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<List<GradingJob>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Where(j => j.CourseId == courseId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }
}
