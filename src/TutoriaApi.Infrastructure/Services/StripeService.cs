using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using TutoriaApi.Core.Interfaces;
using StripeSubscription = Stripe.Subscription;
using StripeInvoice = Stripe.Invoice;
using StripeSubscriptionService = Stripe.SubscriptionService;

namespace TutoriaApi.Infrastructure.Services;

public class StripeService : IStripeService
{
    private readonly StripeClient _client;
    private readonly string _webhookSecret;
    private readonly ILogger<StripeService> _logger;

    public StripeService(IConfiguration configuration, ILogger<StripeService> logger)
    {
        _logger = logger;

        var secretKey = configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured");
        _webhookSecret = configuration["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured");

        _client = new StripeClient(secretKey);
    }

    public async Task<string> CreateCustomerAsync(string email, string universityName)
    {
        var service = new CustomerService(_client);
        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = universityName,
            Metadata = new Dictionary<string, string>
            {
                { "platform", "tutoria" }
            }
        });

        _logger.LogInformation("Created Stripe customer {CustomerId} for {UniversityName}", customer.Id, universityName);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        int subscriptionId,
        string successUrl,
        string cancelUrl)
    {
        var service = new SessionService(_client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = stripePriceId,
                    Quantity = 1
                }
            },
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "subscription_id", subscriptionId.ToString() }
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "subscription_id", subscriptionId.ToString() }
                }
            }
        });

        _logger.LogInformation("Created Stripe checkout session {SessionId} for subscription {SubscriptionId}",
            session.Id, subscriptionId);
        return session.Url;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId)
    {
        var service = new StripeSubscriptionService(_client);
        await service.UpdateAsync(stripeSubscriptionId, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        });

        _logger.LogInformation("Scheduled Stripe subscription {SubscriptionId} for cancellation at period end",
            stripeSubscriptionId);
    }

    public Task<StripeWebhookEvent> ParseWebhookAsync(string rawBody, string signatureHeader)
    {
        var stripeEvent = EventUtility.ConstructEvent(rawBody, signatureHeader, _webhookSecret);

        _logger.LogInformation("Received Stripe webhook: {EventType}", stripeEvent.Type);

        var result = new StripeWebhookEvent
        {
            EventType = stripeEvent.Type
        };

        if (stripeEvent.Data.Object is Session checkoutSession)
        {
            // checkout.session.completed — contains our internal subscription_id in metadata
            // and the Stripe subscription ID that was created
            result.StripeSubscriptionId = checkoutSession.SubscriptionId;
            result.StripeCustomerId = checkoutSession.CustomerId;
            result.Status = checkoutSession.Status;

            if (checkoutSession.Metadata?.TryGetValue("subscription_id", out var internalIdStr) == true
                && int.TryParse(internalIdStr, out var internalId))
            {
                result.InternalSubscriptionId = internalId;
            }
        }
        else if (stripeEvent.Data.Object is StripeSubscription sub)
        {
            result.StripeSubscriptionId = sub.Id;
            result.StripeCustomerId = sub.CustomerId;
            result.Status = sub.Status;

            // Also try to read our internal subscription_id from metadata (set via SubscriptionData)
            if (sub.Metadata?.TryGetValue("subscription_id", out var internalIdStr) == true
                && int.TryParse(internalIdStr, out var internalId))
            {
                result.InternalSubscriptionId = internalId;
            }
        }
        else if (stripeEvent.Data.Object is StripeInvoice invoice)
        {
            result.StripeSubscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
            result.StripeCustomerId = invoice.CustomerId;
            result.Status = invoice.Status;
        }

        return Task.FromResult(result);
    }
}
