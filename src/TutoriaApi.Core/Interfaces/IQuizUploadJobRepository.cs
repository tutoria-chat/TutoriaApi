using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IQuizUploadJobRepository : IRepository<QuizUploadJob>
{
    Task<IEnumerable<QuizUploadJob>> GetByCourseIdAsync(int courseId);
    Task<IEnumerable<QuizUploadJob>> GetByStatusAsync(string status);
    Task<QuizUploadJob?> GetWithCourseAsync(int id);
}
