using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Proxies an on-demand QuizAnalytics rebuild to tutoria-worker
/// (POST /api/v2/analytics/recompute-quiz), the single owner of the aggregation.
/// </summary>
public class QuizAnalyticsAdminService : IQuizAnalyticsAdminService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QuizAnalyticsAdminService> _logger;

    public QuizAnalyticsAdminService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<QuizAnalyticsAdminService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> RecomputeAsync(int? windowDays = null)
    {
        var baseUrl = _configuration["WorkerApi:BaseUrl"]?.TrimEnd('/');
        // The internal key is shared across the Python services; fall back to AiApi's.
        var apiKey = _configuration["WorkerApi:InternalApiKey"] ?? _configuration["AiApi:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("WorkerApi:BaseUrl is not configured");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Internal API key is not configured");

        using var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(120); // a 90-day rebuild over all modules can take a while

        var url = $"{baseUrl}/api/v2/analytics/recompute-quiz";
        if (windowDays.HasValue) url += $"?window_days={windowDays.Value}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("X-Internal-Api-Key", apiKey);

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<RecomputeResult>();
        var modules = payload?.ModulesAggregated ?? 0;
        _logger.LogInformation("Quiz analytics recompute complete: {Modules} modules", modules);
        return modules;
    }

    private class RecomputeResult
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("modules_aggregated")] public int ModulesAggregated { get; set; }
        [JsonPropertyName("window_days")] public int WindowDays { get; set; }
    }
}
