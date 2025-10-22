using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class FileListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? BlobUrl { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public bool IsActive { get; set; }
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }
    // Video/Transcription fields
    public string? SourceType { get; set; }
    public string? SourceUrl { get; set; }
    public string? TranscriptionStatus { get; set; }
    public int? TranscriptWordCount { get; set; }
    public int? VideoDurationSeconds { get; set; }
    public DateTime? TranscriptedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class FileDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? BlobUrl { get; set; }
    public string? BlobContainer { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public bool IsActive { get; set; }
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }
    // Video/Transcription fields
    public string? SourceType { get; set; }
    public string? SourceUrl { get; set; }
    public string? TranscriptionStatus { get; set; }
    public string? TranscriptLanguage { get; set; }
    public int? TranscriptWordCount { get; set; }
    public int? VideoDurationSeconds { get; set; }
    public DateTime? TranscriptedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UploadFileRequest
{
    [Required(ErrorMessage = "Module ID is required")]
    public int ModuleId { get; set; }

    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;
}

public class UpdateFileRequest
{
    [MaxLength(255, ErrorMessage = "File name cannot exceed 255 characters")]
    public string? FileName { get; set; }
}

public class UpdateFileStatusRequest
{
    [Required(ErrorMessage = "IsActive is required")]
    public bool IsActive { get; set; }

    [MaxLength(255, ErrorMessage = "OpenAI File ID cannot exceed 255 characters")]
    public string? OpenAIFileId { get; set; }

    [MaxLength(255, ErrorMessage = "Anthropic File ID cannot exceed 255 characters")]
    public string? AnthropicFileId { get; set; }
}

// YouTube Transcription DTOs

public class AddYoutubeVideoRequest
{
    [Required(ErrorMessage = "YouTube URL is required")]
    [MaxLength(1000, ErrorMessage = "URL cannot exceed 1000 characters")]
    [Url(ErrorMessage = "Please provide a valid URL")]
    [RegularExpression(
        @"^(https?://)?(www\.)?(youtube\.com/(watch\?v=|embed/|shorts/)|youtu\.be/)[a-zA-Z0-9_-]{11}",
        ErrorMessage = "Please provide a valid YouTube URL")]
    public string YoutubeUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Module ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Module ID must be a positive number")]
    public int ModuleId { get; set; }

    [Required(ErrorMessage = "Language is required")]
    [RegularExpression(@"^(pt-br|en|es)$", ErrorMessage = "Language must be one of: pt-br, en, es")]
    public string Language { get; set; } = "pt-br";

    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }
}

public class TranscriptionStatusDto
{
    public int FileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // 'pending', 'processing', 'completed', 'failed'
    public int? WordCount { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Language { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceType { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool HasTranscript { get; set; }
}

public class TranscriptionResultDto
{
    public int FileId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int? DurationSeconds { get; set; }
    public string Source { get; set; } = string.Empty;  // 'youtube_manual', 'youtube_auto', 'whisper'
    public decimal CostUsd { get; set; }
    public string Language { get; set; } = string.Empty;
    public string? TranscriptPreview { get; set; }
}

public class TranscriptTextDto
{
    public int FileId { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public string Language { get; set; } = string.Empty;
}
