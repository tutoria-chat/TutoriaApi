using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IQuizUploadJobService
{
    Task<QuizUploadJob> CreateJobAsync(int courseId, Stream fileStream, string fileName, string contentType, User currentUser);
    Task<List<QuizUploadJob>> GetJobsForCourseAsync(int courseId, User currentUser);
    Task<QuizUploadJob?> GetJobWithQuestionsAsync(int jobId, User currentUser);
}
