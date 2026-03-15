using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class SubscriptionDto
{
    public int Id { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public int PlanId { get; set; }
    public string? PlanName { get; set; }
    public string? PlanSlug { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CheckoutRequest
{
    [Required(ErrorMessage = "Plan slug is required")]
    public string PlanSlug { get; set; } = string.Empty;

    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public class CheckoutResponse
{
    public string CheckoutUrl { get; set; } = string.Empty;
}

public class SetEnterprisePricingRequest
{
    [Required(ErrorMessage = "Custom Stripe Price ID is required")]
    public required string CustomStripePriceId { get; set; }
}

public class UniversityLimitsDto
{
    public int MaxCourses { get; set; }
    public int MaxModules { get; set; }
    public int? MaxStudents { get; set; }
    public int CurrentCourses { get; set; }
    public int CurrentModules { get; set; }
    public int CurrentStudents { get; set; }
    public bool HasAIQuizzes { get; set; }
    public bool HasWhatsApp { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasCustomModelConfig { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanSlug { get; set; } = string.Empty;
}
