using TutoriaApi.Core.Entities;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Core.Interfaces;

// View model for file with module details
public class FileWithDetailsViewModel
{
    public FileEntity File { get; set; } = null!;
    public string? ModuleName { get; set; }
    public string? CourseName { get; set; }
    public string? UniversityName { get; set; }
}

public interface IFileService
{
    Task<(List<FileEntity> Items, int Total)> GetPagedFilesAsync(
        int? moduleId,
        string? search,
        int page,
        int pageSize,
        User currentUser);

    Task<FileWithDetailsViewModel?> GetFileWithDetailsAsync(int id, User currentUser);

    Task<FileEntity> UploadFileAsync(
        int moduleId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string? customName,
        User currentUser);

    Task<FileEntity> UpdateFileAsync(int id, string? newFileName, User currentUser);

    Task<FileEntity> UpdateFileStatusAsync(
        int id,
        string status,
        string? errorMessage,
        string? openAIFileId);

    Task<string> GetDownloadUrlAsync(int id, User currentUser);

    Task DeleteFileAsync(int id, User currentUser);

    Task<bool> CanUserAccessFileAsync(int fileId, User user);

    Task<List<int>> GetAccessibleModuleIdsAsync(User user);
}
