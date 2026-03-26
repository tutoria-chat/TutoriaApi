using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class TopicClassificationRepository : ITopicClassificationRepository
{
    private readonly TutoriaDbContext _context;

    public TopicClassificationRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<TopicClassification>> GetTopTopicsAsync(
        List<int> moduleIds, DateOnly startDate, DateOnly endDate, int limit = 20)
    {
        return await _context.TopicClassifications
            .Where(t => moduleIds.Contains(t.ModuleId)
                        && t.Date >= startDate
                        && t.Date <= endDate)
            .OrderByDescending(t => t.QuestionCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<TopicClassification>> GetByModuleAndDateRangeAsync(
        int moduleId, DateOnly startDate, DateOnly endDate)
    {
        return await _context.TopicClassifications
            .Where(t => t.ModuleId == moduleId
                        && t.Date >= startDate
                        && t.Date <= endDate)
            .OrderByDescending(t => t.QuestionCount)
            .ToListAsync();
    }
}
