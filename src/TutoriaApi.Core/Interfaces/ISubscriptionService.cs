using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ISubscriptionService
{
    Task<Subscription?> GetCurrentByUniversityIdAsync(int universityId);
    Task<Subscription> CreateAsync(Subscription subscription);
    Task<Subscription> UpdateStatusAsync(int id, string status);
    Task<Subscription> CancelAsync(int universityId);
    Task HandleStripeWebhookAsync(string eventType, string subscriptionId, string? status, int? internalSubscriptionId = null);

    /// <summary>
    /// Creates a Stripe checkout session for upgrading/changing plan.
    /// Returns the checkout URL.
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(int universityId, string planSlug, string? successUrl, string? cancelUrl);
}
