using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Repositories;

public class FileRepository : Repository<FileEntity>, IFileRepository
{
    public FileRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<FileEntity?> GetWithModuleAsync(int id)
    {
        return await _dbSet
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<(IEnumerable<FileEntity> Items, int Total)> SearchAsync(
        int? moduleId,
        string? search,
        int page,
        int pageSize,
        List<int>? allowedModuleIds = null)
    {
        var query = _dbSet
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

        // Access control filter
        if (allowedModuleIds != null && allowedModuleIds.Any())
        {
            query = query.Where(f => allowedModuleIds.Contains(f.ModuleId));
        }

        if (moduleId.HasValue)
        {
            query = query.Where(f => f.ModuleId == moduleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(f => f.FileName.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<FileEntity>> GetByModuleIdAsync(int moduleId)
    {
        return await _dbSet
            .Where(f => f.ModuleId == moduleId)
            .ToListAsync();
    }

    public async Task<bool> ExistsByBlobNameAsync(string blobName)
    {
        return await _dbSet.AnyAsync(f => f.BlobPath == blobName);
    }
}
