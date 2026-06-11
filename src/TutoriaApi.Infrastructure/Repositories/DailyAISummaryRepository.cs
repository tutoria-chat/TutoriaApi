using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class DailyAISummaryRepository : Repository<DailyAISummary>, IDailyAISummaryRepository
{
    public DailyAISummaryRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<List<DailyAISummary>> GetRecentByUniversityIdAsync(int universityId, int count = 7)
    {
        return await _dbSet
            .Where(s => s.UniversityId == universityId)
            .OrderByDescending(s => s.Date)
            .Take(count)
            .ToListAsync();
    }
}
