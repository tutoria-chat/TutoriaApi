namespace TutoriaApi.Core.Entities;

/// <summary>
/// Pre-computed quiz performance stats per concept per module.
/// Overwritten nightly by tutoria-worker from DynamoDB QuizAttempts data.
/// </summary>
public class QuizAnalytic : BaseEntity
{
    public int ModuleId { get; set; }
    public int? QuizId { get; set; }
    public required string ConceptName { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public decimal SuccessRate { get; set; }
    public string? Difficulty { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
