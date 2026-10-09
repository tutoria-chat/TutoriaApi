namespace TutoriaApi.Core.Entities;

/// <summary>RevenueCat webhook deliveries already processed (they can repeat).</summary>
public class StudentBillingEvent : BaseEntity
{
    public required string EventId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? AppUserId { get; set; }
    public string? Environment { get; set; }
    public string? Payload { get; set; }
}
