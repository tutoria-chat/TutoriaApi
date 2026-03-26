using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class QuizAnalyticsRepository : IQuizAnalyticsRepository
{
    private readonly TutoriaDbContext _context;

    public QuizAnalyticsRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuizAnalytic>> GetByModuleIdAsync(int moduleId)
    {
        return await _context.QuizAnalytics
            .Where(q => q.ModuleId == moduleId)
            .OrderByDescending(q => q.TotalAttempts)
            .ToListAsync();
    }

    public async Task<List<QuizAnalytic>> GetByModuleIdsAsync(List<int> moduleIds)
    {
        return await _context.QuizAnalytics
            .Where(q => moduleIds.Contains(q.ModuleId))
            .OrderByDescending(q => q.TotalAttempts)
            .ToListAsync();
    }
}
