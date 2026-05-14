using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IAssignmentRepository : IRepository<Assignment>
{
    Task<(List<Assignment> Items, int Total)> GetPagedByModuleIdAsync(int moduleId, int page, int pageSize, bool includeUnpublished = true);
    Task<Assignment?> GetByIdWithModuleAsync(int id);
    Task<List<Assignment>> GetPublishedByModuleIdAsync(int moduleId);
    Task AddContextFilesAsync(IEnumerable<AssignmentContextFile> contextFiles);
}
