namespace TutoriaApi.Core.Interfaces;

public interface IQuizAnalyticsRepository
{
    Task<List<Entities.QuizAnalytic>> GetByModuleIdAsync(int moduleId);
    Task<List<Entities.QuizAnalytic>> GetByModuleIdsAsync(List<int> moduleIds);
}
