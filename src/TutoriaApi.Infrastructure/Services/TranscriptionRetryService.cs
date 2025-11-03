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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranscriptionRetryService> _logger;
    private readonly string _aiApiBaseUrl;

    public TranscriptionRetryService(
        IFileRepository fileRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TranscriptionRetryService> logger)
    {
        _fileRepository = fileRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _aiApiBaseUrl = configuration["AiApi:BaseUrl"] ?? throw new InvalidOperationException("AiApi:BaseUrl not configured");
    }

    public async Task<int> RetryFailedTranscriptionsAsync()
    {
        _logger.LogInformation("🔄 [TranscriptionRetry] Starting daily failed transcription retry job");

        try
        {
            // Get failed transcriptions from last 72 hours
            var failedFiles = await _fileRepository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

            if (!failedFiles.Any())
            {
                _logger.LogInformation("✅ [TranscriptionRetry] No failed transcriptions found from last 72 hours");
                return 0;
            }

            _logger.LogInformation($"📋 [TranscriptionRetry] Found {failedFiles.Count} failed transcriptions to retry");

            int successCount = 0;
            int failureCount = 0;

            foreach (var file in failedFiles)
            {
                try
                {
                    _logger.LogInformation($"🔄 [TranscriptionRetry] Retrying file_id={file.Id}, module_id={file.ModuleId}, video_url={file.SourceUrl}");

                    // Call Python API retry endpoint
                    var result = await CallPythonRetryEndpointAsync(file.Id);

                    if (result)
                    {
                        successCount++;
                        _logger.LogInformation($"✅ [TranscriptionRetry] Successfully retried file_id={file.Id}");
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning($"⚠️ [TranscriptionRetry] Failed to retry file_id={file.Id}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, $"❌ [TranscriptionRetry] Exception while retrying file_id={file.Id}");
                }

                // Small delay between retries to avoid overwhelming the Python API
                await Task.Delay(2000); // 2 seconds between retries
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

    private async Task<bool> CallPythonRetryEndpointAsync(int fileId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_aiApiBaseUrl);
            httpClient.Timeout = TimeSpan.FromMinutes(10); // Long timeout for transcription processing

            var response = await httpClient.PostAsync($"/api/v2/transcription/retry/{fileId}", null);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"[TranscriptionRetry] Retry response for file_id={fileId}: {responseContent}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning($"[TranscriptionRetry] Retry failed for file_id={fileId}, status={response.StatusCode}, error={errorContent}");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"[TranscriptionRetry] HTTP request error for file_id={fileId}");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, $"[TranscriptionRetry] Timeout error for file_id={fileId}");
            return false;
        }
    }
}
