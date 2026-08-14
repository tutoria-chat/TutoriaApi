using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class AssignmentRepository : Repository<Assignment>, IAssignmentRepository
{
    public AssignmentRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<(List<Assignment> Items, int Total)> GetPagedByCourseIdAsync(
        int courseId, int page, int pageSize, bool includeUnpublished = true)
    {
        var query = _dbSet
            .Include(a => a.CreatedBy)
            .Include(a => a.ContextFiles)
            .Where(a => a.CourseId == courseId && a.IsActive);

        if (!includeUnpublished)
            query = query.Where(a => a.IsPublished);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Assignment?> GetByIdWithCourseAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Course)
                .ThenInclude(c => c.University)
            .Include(a => a.CreatedBy)
            .Include(a => a.ContextFiles)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddContextFilesAsync(IEnumerable<AssignmentContextFile> contextFiles)
    {
        await _context.Set<AssignmentContextFile>().AddRangeAsync(contextFiles);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Assignment>> GetPublishedByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Include(a => a.ContextFiles)
            .Where(a => a.CourseId == courseId && a.IsActive && a.IsPublished)
            .OrderBy(a => a.DueDate)
            .ToListAsync();
    }
}
