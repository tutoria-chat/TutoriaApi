using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class EnemBankService : IEnemBankService
{
    // Candidate editions offered for import. The importer (tutoria-api) skips any
    // not yet published upstream, so listing 2025 now is safe.
    private static readonly List<string> CandidateYears = new() { "2022", "2023", "2024", "2025" };
    private static readonly string[] Areas = { "linguagens", "humanas", "natureza", "matematica" };

    private readonly IEnemQuestionRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EnemBankService> _logger;

    public EnemBankService(
        IEnemQuestionRepository repository,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<EnemBankService> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EnemBankStatusResult> GetStatusAsync()
    {
        var counts = await _repository.GetOfficialCountsAsync();

        var byYear = new Dictionary<string, int>();
        var byArea = Areas.ToDictionary(a => a, _ => 0);
        var total = 0;

        foreach (var c in counts)
        {
            total += c.Count;
            if (c.Year.HasValue)
            {
                var y = c.Year.Value.ToString();
                byYear[y] = byYear.GetValueOrDefault(y) + c.Count;
            }
            if (byArea.ContainsKey(c.Area))
                byArea[c.Area] += c.Count;
        }

        return new EnemBankStatusResult
        {
            Total = total,
            ByYear = byYear,
            ByArea = byArea,
            AvailableYears = CandidateYears,
        };
    }

    public async Task TriggerImportAsync(List<string>? years)
    {
        var baseUrl = _configuration["AiApi:BaseUrl"]?.TrimEnd('/');
        var apiKey = _configuration["AiApi:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AiApi BaseUrl / InternalApiKey not configured");

        using var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v2/enem/import");
        req.Headers.Add("X-Internal-Api-Key", apiKey);
        req.Content = JsonContent.Create(new { years });

        var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        _logger.LogInformation("Triggered ENEM import on tutoria-api for years {Years}",
            years is null ? "all" : string.Join(",", years));
    }
}
