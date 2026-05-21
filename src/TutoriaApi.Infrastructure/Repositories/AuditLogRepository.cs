using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(TutoriaDbContext context) : base(context) { }

    public async Task<(IEnumerable<AuditLog> Items, int Total)> SearchAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        int page,
        int pageSize)
    {
        var query = _dbSet.AsQueryable();

        // University scoping for managers
        if (universityId.HasValue)
        {
            query = query.Where(a => a.UniversityId == universityId.Value);
        }

        // Filters
        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a =>
                a.Username.Contains(search) ||
                a.EntityName != null && a.EntityName.Contains(search));
        }

        var total = await query.CountAsync();

        var items = await query
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<AuditLog>> GetAllForExportAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = _dbSet.AsQueryable();

        // Apply same filters as SearchAsync (without pagination)
        if (universityId.HasValue) query = query.Where(a => a.UniversityId == universityId.Value);
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (startDate.HasValue) query = query.Where(a => a.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(a => a.CreatedAt <= endDate.Value);

        return await query
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10000)  // Safety limit for exports
            .ToListAsync();
    }
}
