namespace TutoriaApi.Core.Entities;

public class Plan : BaseEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public decimal MonthlyPriceBRL { get; set; }
    public string? StripePriceId { get; set; }
    public int MaxCourses { get; set; }
    public int MaxModules { get; set; }
    public int? MaxStudents { get; set; }
    public bool HasAIQuizzes { get; set; }
    public bool HasWhatsApp { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasCustomModelConfig { get; set; }
    public int TrialDays { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCustom { get; set; }

    // Navigation properties
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
