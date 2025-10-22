using TutoriaApi.Core.Entities;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Core.Interfaces;

public interface IVideoTranscriptionService
{
    /// <summary>
    /// Submit a YouTube video for transcription
    /// </summary>
    /// <returns>File entity with transcription result</returns>
    Task<FileEntity> TranscribeYoutubeVideoAsync(
        string youtubeUrl,
        int moduleId,
        string language,
        string? customName,
        User currentUser);

    /// <summary>
    /// Get file with transcription status (includes authorization check)
    /// </summary>
    Task<FileEntity?> GetTranscriptionStatusAsync(int fileId, User currentUser);

    /// <summary>
    /// Get file with transcript text (includes authorization check)
    /// </summary>
    Task<FileEntity?> GetTranscriptTextAsync(int fileId, User currentUser);

    /// <summary>
    /// Retry a failed transcription
    /// </summary>
    Task<FileEntity> RetryTranscriptionAsync(int fileId, User currentUser);

    /// <summary>
    /// Delete a transcription (soft delete, includes authorization check)
    /// </summary>
    Task<bool> DeleteTranscriptionAsync(int fileId, User currentUser);
}
