namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Service for retrying failed video transcriptions
/// </summary>
public interface ITranscriptionRetryService
{
    /// <summary>
    /// Retries all failed YouTube transcriptions from the last 72 hours
    /// </summary>
    /// <returns>Number of transcriptions retried</returns>
    Task<int> RetryFailedTranscriptionsAsync();
}
