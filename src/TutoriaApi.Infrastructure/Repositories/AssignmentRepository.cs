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

    public async Task<(List<Assignment> Items, int Total)> GetPagedByModuleIdAsync(
        int moduleId, int page, int pageSize, bool includeUnpublished = true)
    {
        var query = _dbSet
            .Include(a => a.CreatedBy)
            .Include(a => a.ContextFiles)
            .Where(a => a.ModuleId == moduleId && a.IsActive);

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

    public async Task<Assignment?> GetByIdWithModuleAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Module)
                .ThenInclude(m => m.Course)
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

    public async Task<List<Assignment>> GetPublishedByModuleIdAsync(int moduleId)
    {
        return await _dbSet
            .Where(a => a.ModuleId == moduleId && a.IsActive && a.IsPublished)
            .OrderBy(a => a.DueDate)
            .ToListAsync();
    }
}
