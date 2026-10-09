using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class StudentBillingService : IStudentBillingService
{
    private const string RevenueCatApi = "https://api.revenuecat.com/v1";
    private const int HmacToleranceSeconds = 300;
    private static readonly Regex AppUserIdPattern = new(@"^u(\d+)$", RegexOptions.Compiled);

    private readonly IStudentAppRepository _repo;
    private readonly IStudentAppService _studentApp;
    private readonly IHttpClientFactory _http;
    private readonly StudentAppOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<StudentBillingService> _logger;

    public StudentBillingService(
        IStudentAppRepository repo,
        IStudentAppService studentApp,
        IHttpClientFactory http,
        IOptions<StudentAppOptions> options,
        TimeProvider clock,
        ILogger<StudentBillingService> logger)
    {
        _repo = repo;
        _studentApp = studentApp;
        _http = http;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public bool StoreEnabled => !string.IsNullOrEmpty(_options.RevenueCatSecretKey);
    public bool DevMode => _options.BillingDevMode;
    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public static string AppUserId(int userId) => $"u{userId}";

    public static int? UserIdFrom(string? appUserId)
    {
        var m = AppUserIdPattern.Match(appUserId ?? string.Empty);
        return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    // ─── RevenueCat customer → plan ──────────────────────────────────────────

    private static DateTime? ParseDate(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
           && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? d : null;

    private static string? Str(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>
    /// Applies RevenueCat's view of the customer (GET /v1/subscribers response) to the
    /// subscription row. Returns true when a store plan is active. Grace periods
    /// count as access. A "dev" plan (QA) is left alone when the store has nothing.
    /// </summary>
    public static bool ApplyCustomerInfo(StudentSubscription sub, JsonElement info, DateTime nowUtc)
    {
        var subscriber = info.TryGetProperty("subscriber", out var s) ? s : default;
        var entitlements = subscriber.ValueKind == JsonValueKind.Object && subscriber.TryGetProperty("entitlements", out var e) ? e : default;
        var subscriptions = subscriber.ValueKind == JsonValueKind.Object && subscriber.TryGetProperty("subscriptions", out var ss) ? ss : default;

        foreach (var planId in StudentApp.PlanRank)
        {
            if (entitlements.ValueKind != JsonValueKind.Object || !entitlements.TryGetProperty(planId, out var ent)) continue;
            var lifetime = !ent.TryGetProperty("expires_date", out var exp) || exp.ValueKind == JsonValueKind.Null;
            DateTime? until = null;
            if (!lifetime)
            {
                var dates = new[] { ParseDate(ent, "expires_date"), ParseDate(ent, "grace_period_expires_date") }
                    .Where(d => d != null).Select(d => d!.Value).ToList();
                until = dates.Count > 0 ? dates.Max() : DateTime.MinValue;
                if (until <= nowUtc) continue;
            }

            var product = Str(ent, "product_identifier") ?? string.Empty;
            var hasSub = subscriptions.ValueKind == JsonValueKind.Object && subscriptions.TryGetProperty(product, out _);
            var storeSub = hasSub ? subscriptions.GetProperty(product) : default;

            sub.Plan = planId;
            var store = hasSub ? Str(storeSub, "store") : null;
            sub.Source = (store ?? "app_store").Length > 20 ? store![..20] : store ?? "app_store";
            // The latest purchase/renewal starts the cycle (Simples area switches reset with it).
            sub.PeriodStart = (hasSub ? ParseDate(storeSub, "purchase_date") : null) ?? ParseDate(ent, "purchase_date") ?? nowUtc;
            sub.PeriodEnd = until ?? new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            sub.WillRenew = hasSub ? Str(storeSub, "unsubscribe_detected_at") == null : null;
            sub.BillingIssue = hasSub && Str(storeSub, "billing_issues_detected_at") != null;
            return true;
        }

        if (sub.Source != "dev")
        {
            if (sub.PeriodEnd != null && sub.PeriodEnd > nowUtc) sub.PeriodEnd = nowUtc;
            sub.WillRenew = null;
            sub.BillingIssue = false;
        }
        return false;
    }

    private async Task<JsonElement> FetchCustomerAsync(int userId)
    {
        var client = _http.CreateClient(nameof(StudentBillingService));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{RevenueCatApi}/subscribers/{AppUserId(userId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.RevenueCatSecretKey);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"RevenueCat answered {(int)response.StatusCode}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task SyncFromStoreAsync(int userId)
    {
        var info = await FetchCustomerAsync(userId);
        var sub = await _repo.GetSubscriptionAsync(userId) ?? new StudentSubscription { UserId = userId };
        var active = ApplyCustomerInfo(sub, info, UtcNow);
        await _repo.UpsertSubscriptionAsync(sub);
        if (active) await _studentApp.EnsureDefaultAgentsAsync(userId);
    }

    public async Task<StudentMeResponse> SyncAsync(int userId)
    {
        await _studentApp.RequireConsumerStudentAsync(userId);
        if (StoreEnabled)
        {
            try
            {
                await SyncFromStoreAsync(userId);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "RevenueCat sync failed for user {UserId}", userId);
                throw new StudentAppException(503, "billing_unavailable", "Não conseguimos confirmar sua assinatura agora. Tente de novo.");
            }
        }
        return await _studentApp.GetMeAsync(userId);
    }

    // ─── Webhook ─────────────────────────────────────────────────────────────

    private bool ValidSignature(string rawBody, string? header)
    {
        if (string.IsNullOrEmpty(header)) return false;
        try
        {
            var parts = header.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            if (!parts.TryGetValue("t", out var ts) || !parts.TryGetValue("v1", out var sig)) return false;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.RevenueCatWebhookHmacSecret!));
            var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{rawBody}"))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(sig.ToLowerInvariant())))
                return false;
            var age = Math.Abs(_clock.GetUtcNow().ToUnixTimeSeconds() - long.Parse(ts, CultureInfo.InvariantCulture));
            return age <= HmacToleranceSeconds;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IEnumerable<string> Ids(JsonElement evt, string name)
    {
        if (!evt.TryGetProperty(name, out var v)) yield break;
        if (v.ValueKind == JsonValueKind.String) yield return v.GetString()!;
        else if (v.ValueKind == JsonValueKind.Array)
            foreach (var x in v.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) yield return x.GetString()!;
    }

    public async Task<int> HandleWebhookAsync(string rawBody, string? authorization, string? signature)
    {
        var expected = _options.RevenueCatWebhookAuth;
        if (string.IsNullOrEmpty(expected) || authorization == null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization), Encoding.UTF8.GetBytes(expected)))
            return 401;
        if (!string.IsNullOrEmpty(_options.RevenueCatWebhookHmacSecret) && !ValidSignature(rawBody, signature))
            return 401;

        JsonElement evt;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("event", out var e) || e.ValueKind != JsonValueKind.Object) return 400;
            evt = e.Clone();
        }
        catch (JsonException)
        {
            return 400;
        }

        var eventId = Str(evt, "id");
        if (!string.IsNullOrEmpty(eventId) && await _repo.BillingEventExistsAsync(eventId)) return 200; // duplicate delivery

        // Everyone the event touches (transfers move a purchase between accounts).
        var userIds = new[] { "app_user_id", "original_app_user_id", "aliases", "transferred_from", "transferred_to" }
            .SelectMany(n => Ids(evt, n))
            .Select(UserIdFrom)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (StoreEnabled)
        {
            foreach (var userId in userIds)
            {
                try
                {
                    await _studentApp.RequireConsumerStudentAsync(userId);
                }
                catch (StudentAppException)
                {
                    continue; // deleted or unknown account
                }
                try
                {
                    await SyncFromStoreAsync(userId);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    _logger.LogWarning(ex, "RevenueCat unavailable while handling webhook {EventId}", eventId);
                    return 503; // RevenueCat retries later; the event isn't recorded yet
                }
            }
        }

        if (!string.IsNullOrEmpty(eventId))
        {
            await _repo.AddBillingEventAsync(new StudentBillingEvent
            {
                EventId = eventId.Length > 100 ? eventId[..100] : eventId,
                Type = (Str(evt, "type") ?? string.Empty) is var t && t.Length > 40 ? t[..40] : t,
                AppUserId = Str(evt, "app_user_id") is { } a ? (a.Length > 100 ? a[..100] : a) : null,
                Environment = Str(evt, "environment") is { } env ? (env.Length > 20 ? env[..20] : env) : null,
                Payload = rawBody.Length > 20000 ? rawBody[..20000] : rawBody,
            });
        }
        _logger.LogInformation("RevenueCat {Type} for users {Users}", Str(evt, "type"), string.Join(",", userIds));
        return 200;
    }

    // ─── Dev / QA ────────────────────────────────────────────────────────────

    public async Task<StudentMeResponse> DevActivateAsync(int userId, string planId)
    {
        if (!DevMode) throw StudentAppException.NotFound("Recurso");
        await _studentApp.RequireConsumerStudentAsync(userId);
        var plan = StudentApp.FindPlan(planId);
        if (plan == null || !plan.Available)
            throw new StudentAppException(400, "plan_unavailable", "Este plano ainda não está disponível.");
        var now = UtcNow;
        var sub = await _repo.GetSubscriptionAsync(userId) ?? new StudentSubscription { UserId = userId };
        sub.Plan = plan.Id;
        sub.Source = "dev";
        sub.PeriodStart = now;
        sub.PeriodEnd = now.AddDays(30);
        sub.WillRenew = true;
        sub.BillingIssue = false;
        await _repo.UpsertSubscriptionAsync(sub);
        await _studentApp.EnsureDefaultAgentsAsync(userId);
        return await _studentApp.GetMeAsync(userId);
    }

    public async Task<StudentMeResponse> DevCancelAsync(int userId)
    {
        if (!DevMode) throw StudentAppException.NotFound("Recurso");
        await _studentApp.RequireConsumerStudentAsync(userId);
        var sub = await _repo.GetSubscriptionAsync(userId);
        if (sub != null)
        {
            sub.Plan = "none";
            sub.PeriodEnd = null;
            await _repo.UpsertSubscriptionAsync(sub);
        }
        return await _studentApp.GetMeAsync(userId);
    }
}
