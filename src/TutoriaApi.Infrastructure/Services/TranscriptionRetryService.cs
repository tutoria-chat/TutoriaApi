using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Service for retrying failed video transcriptions via background job
/// </summary>
public class TranscriptionRetryService : ITranscriptionRetryService
{
    private readonly IFileRepository _fileRepository;
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly ILogger<TranscriptionRetryService> _logger;
    private readonly int _delayBetweenRetriesMs;
    private readonly int _maxRetryAgeHours;

    public TranscriptionRetryService(
        IFileRepository fileRepository,
        ISqsMessagingService sqsMessagingService,
        IConfiguration configuration,
        ILogger<TranscriptionRetryService> logger)
    {
        _fileRepository = fileRepository;
        _sqsMessagingService = sqsMessagingService;
        _logger = logger;

        _delayBetweenRetriesMs = configuration.GetValue("TranscriptionRetry:DelayBetweenRetriesMs", 2000);
        _maxRetryAgeHours = configuration.GetValue("TranscriptionRetry:MaxRetryAgeHours", 72);

        _logger.LogInformation("TranscriptionRetryService initialized with DelayBetweenRetriesMs={DelayMs}, MaxRetryAgeHours={MaxAge}",
            _delayBetweenRetriesMs, _maxRetryAgeHours);
    }

    public async Task<int> RetryFailedTranscriptionsAsync()
    {
        _logger.LogInformation("🔄 [TranscriptionRetry] Starting daily failed transcription retry job");

        try
        {
            // Get failed transcriptions from configured time window (default: 72 hours)
            var failedFiles = await _fileRepository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

            if (!failedFiles.Any())
            {
                _logger.LogInformation($"✅ [TranscriptionRetry] No failed transcriptions found from last {_maxRetryAgeHours} hours");
                return 0;
            }

            _logger.LogInformation($"📋 [TranscriptionRetry] Found {failedFiles.Count} failed transcriptions to retry");

            int successCount = 0;
            int failureCount = 0;

            foreach (var file in failedFiles)
            {
                try
                {
                    _logger.LogInformation($"🔄 [TranscriptionRetry] Re-queuing file_id={file.Id}, module_id={file.ModuleId}, video_url={file.SourceUrl}");

                    if (string.IsNullOrEmpty(file.SourceUrl))
                    {
                        _logger.LogWarning($"⚠️ [TranscriptionRetry] Skipping file_id={file.Id} — no SourceUrl");
                        failureCount++;
                        continue;
                    }

                    file.TranscriptionStatus = "pending";
                    await _fileRepository.UpdateAsync(file);

                    var sent = await _sqsMessagingService.SendTranscriptionJobAsync(
                        file.Id, file.SourceUrl, file.TranscriptLanguage ?? "pt-br");

                    if (sent)
                    {
                        successCount++;
                        _logger.LogInformation($"✅ [TranscriptionRetry] Re-queued file_id={file.Id}");
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning($"⚠️ [TranscriptionRetry] SQS send failed for file_id={file.Id}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, $"❌ [TranscriptionRetry] Exception while retrying file_id={file.Id}");
                }

                await Task.Delay(_delayBetweenRetriesMs);
            }

            _logger.LogInformation($"🎉 [TranscriptionRetry] Job completed: {successCount} succeeded, {failureCount} failed out of {failedFiles.Count} total");

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TranscriptionRetry] Fatal error during retry job");
            throw;
        }
    }

}
