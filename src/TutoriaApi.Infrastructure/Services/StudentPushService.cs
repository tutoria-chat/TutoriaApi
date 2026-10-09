using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class StudentPushService : IStudentPushService
{
    public const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";

    private readonly IStudentAppRepository _repo;
    private readonly IHttpClientFactory _http;
    private readonly StudentAppOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<StudentPushService> _logger;

    public StudentPushService(
        IStudentAppRepository repo,
        IHttpClientFactory http,
        IOptions<StudentAppOptions> options,
        TimeProvider clock,
        ILogger<StudentPushService> logger)
    {
        _repo = repo;
        _http = http;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task SendAsync(IReadOnlyCollection<int> userIds, string title, string body, IDictionary<string, object>? data, string kind)
    {
        var allowed = new List<int>();
        foreach (var id in userIds.Distinct())
        {
            var profile = await _repo.GetProfileAsync(id);
            if (profile == null) continue;
            if (kind == "streak" ? profile.NotifyStreak : profile.NotifyUpdates) allowed.Add(id);
        }
        if (allowed.Count == 0) return;

        var tokens = await _repo.GetDeviceTokensAsync(allowed);
        var messages = tokens.Select(t => new Dictionary<string, object>
        {
            ["to"] = t.Token,
            ["title"] = title,
            ["body"] = body,
            ["sound"] = "default",
            ["data"] = data ?? new Dictionary<string, object>(),
        }).ToList();
        var dead = await SendToExpoAsync(messages);
        if (dead.Count > 0) await _repo.RemoveDeviceTokensAsync(dead);
    }

    /// <summary>Sends in batches of 100; returns tokens Expo reports as uninstalled.</summary>
    private async Task<List<string>> SendToExpoAsync(List<Dictionary<string, object>> messages)
    {
        var dead = new List<string>();
        var client = _http.CreateClient(nameof(StudentPushService));
        foreach (var batch in messages.Chunk(100))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ExpoPushUrl) { Content = JsonContent.Create(batch) };
                if (!string.IsNullOrEmpty(_options.ExpoAccessToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ExpoAccessToken);
                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Expo push answered {Status} for {Count} messages", (int)response.StatusCode, batch.Length);
                    continue;
                }
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("data", out var tickets) || tickets.ValueKind != JsonValueKind.Array) continue;
                var i = 0;
                foreach (var ticket in tickets.EnumerateArray())
                {
                    if (i >= batch.Length) break;
                    if (ticket.TryGetProperty("status", out var st) && st.GetString() == "error" &&
                        ticket.TryGetProperty("details", out var details) &&
                        details.TryGetProperty("error", out var err) && err.GetString() == "DeviceNotRegistered")
                    {
                        dead.Add((string)batch[i]["to"]);
                    }
                    i++;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Expo push failed for {Count} messages", batch.Length);
            }
        }
        return dead;
    }

    public async Task<int> SendStreakRemindersAsync()
    {
        var university = await _repo.GetConsumerUniversityAsync();
        if (university == null) return 0;
        var today = StudentApp.TodayBrazil(_clock.GetUtcNow().UtcDateTime);
        var candidates = await _repo.GetStreakReminderCandidatesAsync(university.Id, today);
        foreach (var (userId, streak) in candidates)
        {
            var profile = await _repo.GetProfileAsync(userId);
            if (profile == null) continue;
            profile.LastStreakReminder = today;
            await _repo.SaveChangesAsync();
            var days = streak == 1 ? "1 dia" : $"{streak} dias";
            await SendAsync(new[] { userId }, "Não perca sua sequência 🔥",
                $"Você está há {days} seguidos. Uma questão ou um flashcard hoje já mantém a chama acesa.",
                new Dictionary<string, object> { ["type"] = "streak", ["url"] = "/home" }, "streak");
        }
        if (candidates.Count > 0) _logger.LogInformation("Sent {Count} student-app streak reminders", candidates.Count);
        return candidates.Count;
    }
}
