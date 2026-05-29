using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IGradingJobRepository : IRepository<GradingJob>
{
    Task<GradingJob?> GetByIdWithCourseAsync(int id);
    Task<List<GradingJob>> GetByCourseIdAsync(int courseId);
}
