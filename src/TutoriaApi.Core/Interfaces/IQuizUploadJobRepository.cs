using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IQuizUploadJobRepository : IRepository<QuizUploadJob>
{
    Task<IEnumerable<QuizUploadJob>> GetByModuleIdAsync(int moduleId);
    Task<IEnumerable<QuizUploadJob>> GetByStatusAsync(string status);
    Task<QuizUploadJob?> GetWithModuleAsync(int id);
}
