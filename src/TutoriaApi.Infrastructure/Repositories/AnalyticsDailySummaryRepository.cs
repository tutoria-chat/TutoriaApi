using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class AnalyticsDailySummaryRepository : IAnalyticsDailySummaryRepository
{
    private readonly TutoriaDbContext _context;

    public AnalyticsDailySummaryRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<AnalyticsDailySummary>> GetByDateRangeAsync(
        DateOnly startDate, DateOnly endDate, List<int>? moduleIds = null)
    {
        var query = _context.AnalyticsDailySummaries
            .Where(a => a.Date >= startDate && a.Date <= endDate);

        if (moduleIds != null && moduleIds.Any())
        {
            query = query.Where(a => moduleIds.Contains(a.ModuleId));
        }

        return await query
            .OrderBy(a => a.Date)
            .ThenBy(a => a.ModuleId)
            .ToListAsync();
    }

    public async Task<List<AnalyticsDailySummary>> GetByModuleIdAsync(
        int moduleId, DateOnly startDate, DateOnly endDate)
    {
        return await _context.AnalyticsDailySummaries
            .Where(a => a.ModuleId == moduleId && a.Date >= startDate && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }
}
