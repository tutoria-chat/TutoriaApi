using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CourseEventRepository : Repository<CourseEvent>, ICourseEventRepository
{
    public CourseEventRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<List<CourseEvent>> GetByCourseIdAsync(int courseId, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = _dbSet
            .Include(e => e.Module)
            .Where(e => e.CourseId == courseId);

        if (fromUtc.HasValue)
            query = query.Where(e => e.StartsAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(e => e.StartsAtUtc <= toUtc.Value);

        return await query.OrderBy(e => e.StartsAtUtc).ToListAsync();
    }

    public async Task<CourseEvent?> GetByIdWithCourseAsync(int id)
    {
        return await _dbSet
            .Include(e => e.Course)
            .Include(e => e.Module)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<CourseEvent?> GetByAssignmentIdAsync(int assignmentId)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.AssignmentId == assignmentId);
    }

    public async Task<List<int>> GetLinkedAssignmentIdsAsync(int courseId)
    {
        return await _dbSet
            .Where(e => e.CourseId == courseId && e.AssignmentId != null)
            .Select(e => e.AssignmentId!.Value)
            .ToListAsync();
    }
}
