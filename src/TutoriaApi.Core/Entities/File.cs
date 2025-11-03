namespace TutoriaApi.Core.Entities;

public class File : BaseEntity
{
    public required string Name { get; set; }
    public required string FileType { get; set; }
    public string? FileName { get; set; }
    public string? BlobUrl { get; set; }
    public string? BlobContainer { get; set; }
    public string? BlobPath { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public int ModuleId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }

    // Video/Transcription Support
    public string? SourceType { get; set; }  // 'upload', 'youtube', 'url'
    public string? SourceUrl { get; set; }  // YouTube URL or external link
    public string? TranscriptionStatus { get; set; }  // 'pending', 'processing', 'completed', 'failed'
    public string? TranscriptText { get; set; }  // Full transcript text
    public string? TranscriptLanguage { get; set; }  // 'pt-br', 'en', 'es'
    public string? TranscriptJobId { get; set; }  // Background job ID for tracking
    public int? VideoDurationSeconds { get; set; }  // Video/audio duration
    public DateTime? TranscriptedAt { get; set; }  // Transcription completion timestamp
    public int? TranscriptWordCount { get; set; }  // Word count for analytics
    public decimal? TranscriptionCostUSD { get; set; }  // Cost in USD (e.g., AssemblyAI $0.25/hour)

    // Navigation properties
    public Module Module { get; set; } = null!;
}
