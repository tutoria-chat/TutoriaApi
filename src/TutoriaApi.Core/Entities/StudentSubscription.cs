namespace TutoriaApi.Core.Entities;

/// <summary>
/// The B2C student's plan (App Store / Google Play via RevenueCat, or "dev" for
/// QA). Kept in sync from RevenueCat — never trusted from the app. PK = UserId.
/// Institutional plans are per university (Subscription); this is per student.
/// </summary>
public class StudentSubscription
{
    public int UserId { get; set; }
    /// <summary>simples | pro | max | universitario | none</summary>
    public string Plan { get; set; } = "none";
    /// <summary>app_store | play_store | test_store | promotional | dev …</summary>
    public string? Source { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    /// <summary>False once cancelled in the store (access continues until PeriodEnd).</summary>
    public bool? WillRenew { get; set; }
    /// <summary>The store couldn't charge (grace period / on hold).</summary>
    public bool BillingIssue { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
