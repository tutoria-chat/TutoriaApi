using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseEventRepository : IRepository<CourseEvent>
{
    Task<List<CourseEvent>> GetByCourseIdAsync(int courseId, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<CourseEvent?> GetByIdWithCourseAsync(int id);
    Task<CourseEvent?> GetByAssignmentIdAsync(int assignmentId);
    Task<List<int>> GetLinkedAssignmentIdsAsync(int courseId);
}
