namespace TutoriaApi.Core.Interfaces;

public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe customer for a university.
    /// </summary>
    Task<string> CreateCustomerAsync(string email, string universityName);

    /// <summary>
    /// Creates a Stripe Checkout Session for a subscription plan.
    /// Returns the checkout session URL for redirect.
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        int subscriptionId,
        string successUrl,
        string cancelUrl);

    /// <summary>
    /// Cancels a Stripe subscription at period end.
    /// </summary>
    Task CancelSubscriptionAsync(string stripeSubscriptionId);

    /// <summary>
    /// Verifies and parses a Stripe webhook event from raw body + signature header.
    /// Returns (eventType, stripeSubscriptionId, status, currentPeriodEnd).
    /// </summary>
    Task<StripeWebhookEvent> ParseWebhookAsync(string rawBody, string signatureHeader);
}

public class StripeWebhookEvent
{
    public required string EventType { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? Status { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>
    /// Internal (DB) subscription ID from checkout metadata.
    /// Used for checkout.session.completed to correlate the Stripe subscription back to our row.
    /// </summary>
    public int? InternalSubscriptionId { get; set; }
}
