using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Service for triggering quiz generation via tutoria-api (Python AI service).
/// </summary>
public class QuizGenerationService : IQuizGenerationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuizGenerationService> _logger;
    private readonly string _aiApiBaseUrl;

    public QuizGenerationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<QuizGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _aiApiBaseUrl = configuration["AiApi:BaseUrl"]
            ?? throw new InvalidOperationException("AiApi:BaseUrl configuration is missing");
    }

    /// <inheritdoc />
    public async Task<bool> TriggerQuizGenerationAsync(int moduleId, int count = 50, bool upsert = true)
    {
        try
        {
            _logger.LogInformation(
                "📝 Triggering quiz generation for module {ModuleId} (count: {Count}, upsert: {Upsert})",
                moduleId,
                count,
                upsert);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Quiz generation can take time

            var url = $"{_aiApiBaseUrl}/api/v2/modules/{moduleId}/generate-quizzes?count={count}&upsert={upsert.ToString().ToLower()}";

            var response = await httpClient.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation(
                    "✅ Successfully triggered quiz generation for module {ModuleId}. Response: {Response}",
                    moduleId,
                    content);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "⚠️ Quiz generation request failed for module {ModuleId}. Status: {StatusCode}, Error: {Error}",
                    moduleId,
                    response.StatusCode,
                    errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "❌ Network error triggering quiz generation for module {ModuleId}. AI API may be unreachable at {BaseUrl}",
                moduleId,
                _aiApiBaseUrl);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(
                ex,
                "⏱️ Quiz generation request timed out for module {ModuleId}",
                moduleId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ Unexpected error triggering quiz generation for module {ModuleId}",
                moduleId);
            return false;
        }
    }
}
