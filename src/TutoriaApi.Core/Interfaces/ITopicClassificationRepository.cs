namespace TutoriaApi.Core.Interfaces;

public interface ITopicClassificationRepository
{
    Task<List<Entities.TopicClassification>> GetTopTopicsAsync(
        List<int> moduleIds, DateOnly startDate, DateOnly endDate, int limit = 20);
    Task<List<Entities.TopicClassification>> GetByModuleAndDateRangeAsync(
        int moduleId, DateOnly startDate, DateOnly endDate);
}
