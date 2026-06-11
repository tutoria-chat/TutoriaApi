using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IDailyAISummaryRepository : IRepository<DailyAISummary>
{
    Task<List<DailyAISummary>> GetRecentByUniversityIdAsync(int universityId, int count = 7);
}
