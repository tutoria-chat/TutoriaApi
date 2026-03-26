namespace TutoriaApi.Core.Entities;

/// <summary>
/// AI-classified question topics per module per day. Written nightly by tutoria-worker.
/// </summary>
public class TopicClassification : BaseEntity
{
    public int ModuleId { get; set; }
    public DateOnly Date { get; set; }
    public required string TopicName { get; set; }
    public int QuestionCount { get; set; }

    /// <summary>
    /// JSON array of up to 3 sample questions for this topic.
    /// </summary>
    public string? SampleQuestions { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
