using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IStripeService _stripeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ISubscriptionRepository subscriptionRepository,
        IPlanRepository planRepository,
        IUniversityRepository universityRepository,
        IStripeService stripeService,
        IConfiguration configuration,
        ILogger<SubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _universityRepository = universityRepository;
        _stripeService = stripeService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Subscription?> GetCurrentByUniversityIdAsync(int universityId)
    {
        return await _subscriptionRepository.GetActiveByUniversityIdAsync(universityId);
    }

    public async Task<Subscription> CreateAsync(Subscription subscription)
    {
        var university = await _universityRepository.GetByIdAsync(subscription.UniversityId);
        if (university == null)
            throw new InvalidOperationException("University not found");

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
        if (plan == null)
            throw new InvalidOperationException("Plan not found");

        return await _subscriptionRepository.AddAsync(subscription);
    }

    public async Task<string> CreateCheckoutSessionAsync(int universityId, string planSlug, string? successUrl, string? cancelUrl)
    {
        var university = await _universityRepository.GetByIdAsync(universityId);
        if (university == null)
            throw new InvalidOperationException("University not found");

        var plan = await _planRepository.GetBySlugAsync(planSlug);
        if (plan == null)
            throw new KeyNotFoundException($"Plan '{planSlug}' not found");

        if (!plan.IsActive)
            throw new InvalidOperationException("This plan is no longer available");

        if (plan.IsCustom)
            throw new InvalidOperationException("Enterprise plans require contacting sales");

        if (string.IsNullOrEmpty(plan.StripePriceId))
            throw new InvalidOperationException($"Plan '{plan.Name}' does not have a Stripe Price ID configured");

        // Create or reuse Stripe customer
        var stripeCustomerId = university.StripeCustomerId;
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            stripeCustomerId = await _stripeService.CreateCustomerAsync(
                university.ContactEmail ?? $"admin@{university.Code.ToLower()}.edu",
                university.Name);

            university.StripeCustomerId = stripeCustomerId;
            await _universityRepository.UpdateAsync(university);
        }

        // Upsert subscription: reuse existing row (UNIQUE constraint on UniversityId)
        // Use 'incomplete' status (allowed by CHECK constraint) for checkout-in-progress
        var existing = await _subscriptionRepository.GetByUniversityIdAsync(universityId);
        Subscription created;
        if (existing != null)
        {
            // Update existing subscription for retry / plan change
            existing.PlanId = plan.Id;
            existing.Status = "incomplete";
            existing.StripeCustomerId = stripeCustomerId;
            await _subscriptionRepository.UpdateAsync(existing);
            created = existing;
        }
        else
        {
            var subscription = new Subscription
            {
                UniversityId = universityId,
                PlanId = plan.Id,
                Status = "incomplete",
                StripeCustomerId = stripeCustomerId
            };
            created = await _subscriptionRepository.AddAsync(subscription);
        }

        var defaultSuccessUrl = _configuration["Stripe:SuccessUrl"] ?? "http://localhost:3000/dashboard?checkout=success";
        var defaultCancelUrl = _configuration["Stripe:CancelUrl"] ?? "http://localhost:3000/dashboard?checkout=canceled";

        var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
            stripeCustomerId,
            plan.StripePriceId,
            created.Id,
            successUrl ?? defaultSuccessUrl,
            cancelUrl ?? defaultCancelUrl);

        _logger.LogInformation("Created checkout session for university {UniversityId} plan {PlanSlug}",
            universityId, planSlug);

        return checkoutUrl;
    }

    public async Task<Subscription> UpdateStatusAsync(int id, string status)
    {
        var existing = await _subscriptionRepository.GetByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException("Subscription not found");

        existing.Status = status;
        if (status == "canceled")
            existing.CanceledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task<Subscription> CancelAsync(int universityId)
    {
        var subscription = await _subscriptionRepository.GetActiveByUniversityIdAsync(universityId);
        if (subscription == null)
            throw new KeyNotFoundException("No active subscription found for this university");

        // Cancel in Stripe if there's a Stripe subscription
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            await _stripeService.CancelSubscriptionAsync(subscription.StripeSubscriptionId);
            _logger.LogInformation("Canceled Stripe subscription {StripeSubId} for university {UniversityId}",
                subscription.StripeSubscriptionId, universityId);
        }

        subscription.Status = "canceled";
        subscription.CanceledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscription);
        return subscription;
    }

    public async Task HandleStripeWebhookAsync(string eventType, string subscriptionId, string? status, int? internalSubscriptionId = null)
    {
        Subscription? subscription = null;

        // For checkout.session.completed, correlate via internal ID and write StripeSubscriptionId
        if (eventType == "checkout.session.completed" && internalSubscriptionId.HasValue)
        {
            subscription = await _subscriptionRepository.GetWithPlanAsync(internalSubscriptionId.Value);
            if (subscription != null && !string.IsNullOrEmpty(subscriptionId))
            {
                subscription.StripeSubscriptionId = subscriptionId;
                subscription.Status = "active";
                subscription.CurrentPeriodStart = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription);
                _logger.LogInformation(
                    "Correlated Stripe subscription {StripeSubId} with internal subscription {InternalId}",
                    subscriptionId, internalSubscriptionId.Value);
                return;
            }
        }

        // Standard lookup by Stripe subscription ID
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            subscription = await _subscriptionRepository.GetByStripeSubscriptionIdAsync(subscriptionId);
        }

        // Fallback: try internal ID from metadata (e.g., customer.subscription.created with metadata)
        if (subscription == null && internalSubscriptionId.HasValue)
        {
            subscription = await _subscriptionRepository.GetWithPlanAsync(internalSubscriptionId.Value);
            if (subscription != null && !string.IsNullOrEmpty(subscriptionId))
            {
                // Backfill StripeSubscriptionId for future lookups
                subscription.StripeSubscriptionId = subscriptionId;
            }
        }

        if (subscription == null)
            throw new KeyNotFoundException($"Subscription with Stripe ID '{subscriptionId}' not found");

        switch (eventType)
        {
            case "customer.subscription.created":
                subscription.Status = SanitizeStripeStatus(status ?? "active");
                break;

            case "customer.subscription.updated":
                if (!string.IsNullOrEmpty(status))
                    subscription.Status = SanitizeStripeStatus(status);
                break;

            case "customer.subscription.deleted":
                subscription.Status = "canceled";
                subscription.CanceledAt = DateTime.UtcNow;
                break;

            case "invoice.payment_succeeded":
                subscription.Status = "active";
                break;

            case "invoice.payment_failed":
                subscription.Status = "past_due";
                break;
        }

        await _subscriptionRepository.UpdateAsync(subscription);
    }

    /// <summary>
    /// Maps Stripe subscription statuses to values allowed by the DB CHECK constraint.
    /// DB allows: active, trialing, past_due, canceled, incomplete, expired.
    /// Stripe can also send: incomplete_expired, paused, unpaid — which must be mapped.
    /// </summary>
    private static string SanitizeStripeStatus(string stripeStatus)
    {
        var allowed = new HashSet<string> { "active", "trialing", "past_due", "canceled", "incomplete", "expired" };

        if (allowed.Contains(stripeStatus))
            return stripeStatus;

        // Map unsupported Stripe statuses to the closest valid DB status
        return stripeStatus switch
        {
            "incomplete_expired" => "expired",
            "unpaid" => "past_due",
            "paused" => "canceled",
            _ => "incomplete" // Safe fallback for any unknown future Stripe status
        };
    }
}
