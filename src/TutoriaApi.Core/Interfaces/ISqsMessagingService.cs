namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Sends fire-and-forget job messages to SQS queues consumed by tutoria-worker.
/// </summary>
public interface ISqsMessagingService
{
    /// <summary>
    /// Enqueue a document text extraction job.
    /// tutoria-worker picks it up and writes extracted text back to File.extracted_text.
    /// </summary>
    Task<bool> SendExtractionJobAsync(int fileId, int moduleId);

    /// <summary>
    /// Enqueue a quiz generation job for a module.
    /// tutoria-worker picks it up and stores generated questions in the Quiz table.
    /// </summary>
    Task<bool> SendQuizGenerationJobAsync(int moduleId, int count = 50, bool upsert = true);

    /// <summary>
    /// Enqueue a YouTube transcription job for an existing pending File record.
    /// tutoria-worker picks it up, downloads/transcribes the video, and writes transcript back to File.
    /// </summary>
    Task<bool> SendTranscriptionJobAsync(int fileId, string youtubeUrl, string language);
}
