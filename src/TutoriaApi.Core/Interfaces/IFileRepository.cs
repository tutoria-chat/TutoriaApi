using TutoriaApi.Core.Entities;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Core.Interfaces;

public interface IFileRepository : IRepository<FileEntity>
{
    Task<FileEntity?> GetWithModuleAsync(int id);
    Task<(IEnumerable<FileEntity> Items, int Total)> SearchAsync(
        int? moduleId,
        string? search,
        int page,
        int pageSize,
        List<int>? allowedModuleIds = null);
    Task<List<FileEntity>> GetByModuleIdAsync(int moduleId);
    Task<bool> ExistsByBlobNameAsync(string blobName);
    Task<List<FileEntity>> GetFailedYoutubeTranscriptionsFromLast72HoursAsync();
    Task<List<FileEntity>> GetCompletedTranscriptionsAsync(
        List<int> moduleIds,
        DateTime? startDate = null,
        DateTime? endDate = null);
}
