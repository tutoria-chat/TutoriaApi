using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IQuizUploadJobService
{
    Task<QuizUploadJob> CreateJobAsync(int moduleId, Stream fileStream, string fileName, string contentType, User currentUser);
    Task<List<QuizUploadJob>> GetJobsForModuleAsync(int moduleId, User currentUser);
    Task<QuizUploadJob?> GetJobWithQuestionsAsync(int jobId, User currentUser);
}
