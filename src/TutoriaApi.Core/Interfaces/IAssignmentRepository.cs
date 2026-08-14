using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IAssignmentRepository : IRepository<Assignment>
{
    Task<(List<Assignment> Items, int Total)> GetPagedByCourseIdAsync(int courseId, int page, int pageSize, bool includeUnpublished = true);
    Task<Assignment?> GetByIdWithCourseAsync(int id);
    Task<List<Assignment>> GetPublishedByCourseIdAsync(int courseId);
    Task AddContextFilesAsync(IEnumerable<AssignmentContextFile> contextFiles);
}
