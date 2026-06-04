using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public record ContextFileUpload(Stream Stream, string FileName, string ContentType, long Size);

public class AssignmentWithDownloadUrl
{
    public Assignment Assignment { get; set; } = null!;
    public string? DownloadUrl { get; set; }
    public string? RubricDownloadUrl { get; set; }
    public List<(AssignmentContextFile File, string DownloadUrl)> ContextFiles { get; set; } = [];
}

public interface IAssignmentService
{
    Task<(List<Assignment> Items, int Total)> GetPagedAsync(int moduleId, int page, int pageSize, User currentUser);
    Task<List<Assignment>> GetPublishedByCourseAsync(int courseId, User currentUser);
    Task<AssignmentWithDownloadUrl?> GetByIdAsync(int id, User currentUser);
    Task<Assignment> CreateAsync(int moduleId, string title, string? description, DateTime dueDate,
        string? keywords, string? gradingCriteria,
        Stream fileStream, string originalFileName, string contentType, long fileSize,
        Stream? rubricStream, string? rubricFileName, string? rubricContentType, long? rubricSize,
        User currentUser,
        List<ContextFileUpload>? contextFiles = null);
    Task<Assignment> UpdateAsync(int id, string title, string? description, DateTime dueDate,
        string? keywords, string? gradingCriteria, User currentUser);
    Task DeleteAsync(int id, User currentUser);
    Task<Assignment> TogglePublishAsync(int id, User currentUser);
}
