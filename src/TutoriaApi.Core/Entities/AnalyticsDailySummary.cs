using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Core.Entities;

/// <summary>
/// Pre-computed daily analytics per module. Written nightly by tutoria-worker.
/// Composite PK (ModuleId, Date) — does NOT inherit BaseEntity.
/// </summary>
public class AnalyticsDailySummary : IAuditable
{
    // Composite primary key
    public int ModuleId { get; set; }
    public DateOnly Date { get; set; }

    // Aggregated metrics
    public int QuestionCount { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public long TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int? AvgResponseTimeMs { get; set; }

    // Top usage
    public string? TopProvider { get; set; }
    public string? TopModel { get; set; }

    // Audit
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
