using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class EnemQuestionRepository : Repository<EnemQuestion>, IEnemQuestionRepository
{
    public EnemQuestionRepository(TutoriaDbContext context) : base(context) { }

    public async Task<List<EnemOfficialCount>> GetOfficialCountsAsync()
    {
        return await _dbSet
            .Where(q => q.Source == "official" && q.IsActive)
            .GroupBy(q => new { q.Year, q.Area })
            .Select(g => new EnemOfficialCount
            {
                Year = g.Key.Year,
                Area = g.Key.Area,
                Count = g.Count(),
            })
            .ToListAsync();
    }
}
