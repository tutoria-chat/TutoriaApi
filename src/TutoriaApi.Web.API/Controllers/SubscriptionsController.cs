using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages university subscriptions and Stripe integration.
/// </summary>
[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : BaseAuthController
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IStripeService _stripeService;
    private readonly TutoriaDbContext _dbContext;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        ISubscriptionService subscriptionService,
        IStripeService stripeService,
        TutoriaDbContext dbContext,
        ILogger<SubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _stripeService = stripeService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get the current university's active subscription.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> GetCurrentSubscription()
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser?.UniversityId == null)
                return BadRequest(new { message = "User is not associated with a university" });

            var subscription = await _subscriptionService.GetCurrentByUniversityIdAsync(currentUser.UniversityId.Value);
            if (subscription == null)
                return NotFound(new { message = "No active subscription found" });

            return Ok(MapToDto(subscription));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current subscription");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get the current university's usage limits based on their plan.
    /// </summary>
    [HttpGet("limits")]
    [ProducesResponseType(typeof(UniversityLimitsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UniversityLimitsDto>> GetUniversityLimits()
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser?.UniversityId == null)
                return BadRequest(new { message = "User is not associated with a university" });

            var universityId = currentUser.UniversityId.Value;
            var subscription = await _subscriptionService.GetCurrentByUniversityIdAsync(universityId);

            var coursesUsed = await _dbContext.Courses.CountAsync(c => c.UniversityId == universityId);
            var modulesUsed = await _dbContext.Modules
                .CountAsync(m => m.Course.UniversityId == universityId);

            // Count distinct students enrolled in courses for this university
            var courseIds = await _dbContext.Courses
                .Where(c => c.UniversityId == universityId)
                .Select(c => c.Id)
                .ToListAsync();
            var studentsUsed = await _dbContext.StudentCourses
                .Where(sc => courseIds.Contains(sc.CourseId))
                .Select(sc => sc.StudentId)
                .Distinct()
                .CountAsync();

            // Check for university-level overrides
            var university = await _dbContext.Universities.FindAsync(universityId);
            var maxCourses = university?.MaxCourses ?? subscription?.Plan?.MaxCourses ?? 3;
            var maxModules = university?.MaxModules ?? subscription?.Plan?.MaxModules ?? 12;
            var maxStudents = university?.MaxStudents ?? subscription?.Plan?.MaxStudents;

            var limits = new UniversityLimitsDto
            {
                MaxCourses = maxCourses,
                MaxModules = maxModules,
                MaxStudents = maxStudents,
                CurrentCourses = coursesUsed,
                CurrentModules = modulesUsed,
                CurrentStudents = studentsUsed,
                HasAIQuizzes = subscription?.Plan?.HasAIQuizzes ?? false,
                HasWhatsApp = subscription?.Plan?.HasWhatsApp ?? false,
                HasPrioritySupport = subscription?.Plan?.HasPrioritySupport ?? false,
                HasCustomModelConfig = subscription?.Plan?.HasCustomModelConfig ?? false,
                PlanName = subscription?.Plan?.Name ?? string.Empty,
                PlanSlug = subscription?.Plan?.Slug ?? string.Empty
            };

            // Compute over-limit IDs (newest items that exceed the limit)
            if (coursesUsed > maxCourses)
            {
                limits.OverLimitCourseIds = await _dbContext.Courses
                    .Where(c => c.UniversityId == universityId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(coursesUsed - maxCourses)
                    .Select(c => c.Id)
                    .ToListAsync();
            }

            if (modulesUsed > maxModules)
            {
                limits.OverLimitModuleIds = await _dbContext.Modules
                    .Where(m => m.Course.UniversityId == universityId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(modulesUsed - maxModules)
                    .Select(m => m.Id)
                    .ToListAsync();
            }

            if (maxStudents.HasValue && studentsUsed > maxStudents.Value)
            {
                // Get the newest student IDs that exceed the limit
                var allStudentIds = await _dbContext.StudentCourses
                    .Where(sc => courseIds.Contains(sc.CourseId))
                    .Select(sc => sc.StudentId)
                    .Distinct()
                    .ToListAsync();

                limits.OverLimitStudentIds = await _dbContext.Users
                    .Where(u => allStudentIds.Contains(u.UserId) && u.UserType == "student")
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(studentsUsed - maxStudents.Value)
                    .Select(u => u.UserId)
                    .ToListAsync();
            }

            return Ok(limits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting university limits");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a Stripe checkout session for a subscription plan.
    /// Returns a checkout URL for redirect.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckout([FromBody] CheckoutRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser?.UniversityId == null)
                return BadRequest(new { message = "User is not associated with a university" });

            var checkoutUrl = await _subscriptionService.CreateCheckoutSessionAsync(
                currentUser.UniversityId.Value,
                request.PlanSlug,
                request.SuccessUrl,
                request.CancelUrl);

            _logger.LogInformation("Created checkout session for university {UniversityId} plan {PlanSlug}",
                currentUser.UniversityId.Value, request.PlanSlug);

            return Ok(new CheckoutResponse { CheckoutUrl = checkoutUrl });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Handle Stripe webhook events.
    /// Reads raw body + Stripe-Signature header for verification.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> HandleWebhook()
    {
        try
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var rawBody = await reader.ReadToEndAsync();
            var signature = HttpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
                return BadRequest(new { message = "Missing Stripe-Signature header" });

            var webhookEvent = await _stripeService.ParseWebhookAsync(rawBody, signature);

            if (!string.IsNullOrEmpty(webhookEvent.StripeSubscriptionId) || webhookEvent.InternalSubscriptionId.HasValue)
            {
                await _subscriptionService.HandleStripeWebhookAsync(
                    webhookEvent.EventType,
                    webhookEvent.StripeSubscriptionId ?? string.Empty,
                    webhookEvent.Status,
                    webhookEvent.InternalSubscriptionId);
            }

            _logger.LogInformation("Processed Stripe webhook: {EventType}", webhookEvent.EventType);
            return Ok(new { message = "Webhook processed successfully" });
        }
        catch (Exception ex) when (ex.GetType().FullName?.StartsWith("Stripe.") == true)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature");
            return BadRequest(new { message = "Invalid webhook signature" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Subscription not found for webhook event");
            return Ok(new { message = "Subscription not found, skipping" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Cancel the current university's subscription.
    /// </summary>
    [HttpPost("cancel")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> CancelSubscription()
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser?.UniversityId == null)
                return BadRequest(new { message = "User is not associated with a university" });

            var subscription = await _subscriptionService.CancelAsync(currentUser.UniversityId.Value);

            _logger.LogInformation("Canceled subscription for university {UniversityId}", currentUser.UniversityId.Value);

            return Ok(MapToDto(subscription));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "No active subscription found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling subscription");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get all subscriptions with university and plan details (SuperAdmin only).
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(List<SubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SubscriptionDto>>> GetAllSubscriptions()
    {
        try
        {
            var subscriptions = await _dbContext.Subscriptions
                .Include(s => s.University)
                .Include(s => s.Plan)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var dtos = subscriptions.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all subscriptions");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Set custom Stripe pricing for a university's enterprise subscription (SuperAdmin only).
    /// </summary>
    [HttpPut("{universityId}/enterprise-pricing")]
    [Authorize(Roles = "super_admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnterprisePricing(int universityId, [FromBody] SetEnterprisePricingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var subscription = await _subscriptionService.SetEnterprisePricingAsync(
                universityId, request.CustomStripePriceId);

            _logger.LogInformation("Set enterprise pricing for university {UniversityId}", universityId);

            return Ok(new { message = "Enterprise pricing configured successfully", subscriptionId = subscription.Id });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting enterprise pricing for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static SubscriptionDto MapToDto(Subscription subscription)
    {
        return new SubscriptionDto
        {
            Id = subscription.Id,
            UniversityId = subscription.UniversityId,
            UniversityName = subscription.University?.Name,
            PlanId = subscription.PlanId,
            PlanName = subscription.Plan?.Name,
            PlanSlug = subscription.Plan?.Slug,
            Status = subscription.Status,
            StripeSubscriptionId = subscription.StripeSubscriptionId,
            StripeCustomerId = subscription.StripeCustomerId,
            CurrentPeriodStart = subscription.CurrentPeriodStart,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            TrialEndsAt = subscription.TrialEndsAt,
            CanceledAt = subscription.CanceledAt,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt
        };
    }
}
