using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TutoriaApi.Web.API.DTOs;

public class CalendarImportJobDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Status { get; set; } = "pending";
    public int ExtractedCount { get; set; }
    public int ConfirmedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OriginalFilename { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>The AI-extracted events for review (present once status = completed).</summary>
    public List<ExtractedCalendarEventDto> Events { get; set; } = new();
}

/// <summary>One AI-extracted candidate event (what the worker stored as JSON).</summary>
public class ExtractedCalendarEventDto
{
    public string Title { get; set; } = string.Empty;
    public string EventType { get; set; } = "other";
    /// <summary>YYYY-MM-DD (local / São Paulo).</summary>
    public string? Date { get; set; }
    /// <summary>HH:MM (local / São Paulo), optional.</summary>
    public string? Time { get; set; }
    public string? Description { get; set; }
}

public class CreateCalendarImportJobRequest
{
    [Required]
    public int CourseId { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}

public class CreateCalendarImportFromUrlRequest
{
    [Required]
    public int CourseId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string SourceUrl { get; set; } = string.Empty;
}

public class ConfirmCalendarImportRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one event is required")]
    public List<ConfirmCalendarEventItem> Events { get; set; } = new();
}

/// <summary>A professor-reviewed event, ready to become a CourseEvent (times already UTC).</summary>
public class ConfirmCalendarEventItem
{
    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public string EventType { get; set; } = "other";

    [Required]
    public DateTime StartsAtUtc { get; set; }

    public DateTime? EndsAtUtc { get; set; }
    public int? ModuleId { get; set; }
    public bool Remind7Days { get; set; }
    public bool Remind3Days { get; set; }
    public bool Remind2Days { get; set; }
    public bool Remind24Hours { get; set; }
}
