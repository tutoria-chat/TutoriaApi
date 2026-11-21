namespace TutoriaApi.Core.Entities;

// The Postgres tables that replace DynamoDB and SQS (tutoria-api docs/MULTICLOUD.md).
// tutoria-api and tutoria-worker write and read them with plain SQL; this API owns
// their schema through EF migrations like every other table. None of them has
// UpdatedAt, so they are not BaseEntity.

/// <summary>One question + answer of a chat ("ChatHistory", was DynamoDB ChatMessages).</summary>
public class ChatHistoryEntry
{
    public long Id { get; set; }
    public string ConversationId { get; set; } = "";
    /// <summary>Epoch milliseconds, the DynamoDB sort key.</summary>
    public long Ts { get; set; }
    public string MessageId { get; set; } = "";
    public int StudentId { get; set; }
    public int ModuleId { get; set; }
    public string Question { get; set; } = "";
    public string Response { get; set; } = "";
    public string? ModelUsed { get; set; }
    public string? Provider { get; set; }
    public bool HasFile { get; set; }
    /// <summary>The student's upload name: any length.</summary>
    public string? FileName { get; set; }
    public int? TokenCount { get; set; }
    public int? ResponseTime { get; set; }
    public string? Matricula { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>90 days after the write: reads skip it, tutoria-api deletes it.</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>One answered quiz question ("QuizAttemptAnswers", was DynamoDB QuizAttempts).</summary>
public class QuizAttemptAnswer
{
    public long Id { get; set; }
    public string AttemptId { get; set; } = "";
    public int ModuleId { get; set; }
    public long Ts { get; set; }
    public int StudentId { get; set; }
    public int? QuizId { get; set; }
    public int QuestionNumber { get; set; }
    public string SelectedAnswer { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string ConceptsCovered { get; set; } = "";
    public string Difficulty { get; set; } = "medium";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>A job for tutoria-worker ("WorkerJobs", was the SQS queues).</summary>
public class WorkerJob
{
    public long Id { get; set; }
    /// <summary>extraction | quiz_gen | transcription | grading | quiz_upload | feedback</summary>
    public string Kind { get; set; } = "";
    /// <summary>The SQS message JSON; NULL once the job ends (learning notes are never kept).</summary>
    public string? Payload { get; set; }
    /// <summary>queued | processing | done | failed</summary>
    public string State { get; set; } = "queued";
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? LastError { get; set; }
}
