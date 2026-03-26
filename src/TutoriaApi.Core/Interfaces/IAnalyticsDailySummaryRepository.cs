namespace TutoriaApi.Core.Interfaces;

public interface IAnalyticsDailySummaryRepository
{
    Task<List<Entities.AnalyticsDailySummary>> GetByDateRangeAsync(
        DateOnly startDate, DateOnly endDate, List<int>? moduleIds = null);
    Task<List<Entities.AnalyticsDailySummary>> GetByModuleIdAsync(
        int moduleId, DateOnly startDate, DateOnly endDate);
}
