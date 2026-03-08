namespace TutoriaApi.Core.Entities;

public class Subscription : BaseEntity
{
    public int UniversityId { get; set; }
    public int PlanId { get; set; }
    public required string Status { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    // Navigation properties
    public University University { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
}
