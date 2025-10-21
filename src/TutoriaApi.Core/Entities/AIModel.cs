namespace TutoriaApi.Core.Entities;

public class AIModel : BaseEntity
{
    // Model identification
    public required string ModelName { get; set; }
    public required string DisplayName { get; set; }
    public required string Provider { get; set; }

    // Model capabilities
    public int MaxTokens { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsFunctionCalling { get; set; }

    // Pricing (per 1M tokens in USD)
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }

    // Subscription tier (1 = Basic/Deprecated, 2 = Standard, 3 = Premium)
    public int RequiredTier { get; set; } = 3;

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsDeprecated { get; set; }
    public DateTime? DeprecationDate { get; set; }

    // Metadata
    public string? Description { get; set; }
    public string? RecommendedFor { get; set; }

    // Navigation properties
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}
