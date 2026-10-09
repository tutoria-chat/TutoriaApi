using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// TutorIA Estudantes store subscriptions (App Store / Google Play via RevenueCat).
/// The RevenueCat customer id is "u{userId}"; each plan is an entitlement whose id
/// equals the plan id. Webhooks and app syncs re-read the customer from RevenueCat.
/// </summary>
public interface IStudentBillingService
{
    bool StoreEnabled { get; }
    bool DevMode { get; }

    Task<StudentMeResponse> SyncAsync(int userId);

    /// <summary>Returns the HTTP status to answer RevenueCat with (200 / 400 / 401 / 503).</summary>
    Task<int> HandleWebhookAsync(string rawBody, string? authorization, string? signature);

    Task<StudentMeResponse> DevActivateAsync(int userId, string planId);
    Task<StudentMeResponse> DevCancelAsync(int userId);
}
