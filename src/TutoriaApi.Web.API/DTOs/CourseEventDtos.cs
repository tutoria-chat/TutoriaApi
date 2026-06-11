using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class CourseEventDto
{
    /// <summary>Null for synthesized assignment entries (no explicit event yet).</summary>
    public int? Id { get; set; }
    public int CourseId { get; set; }
    public int? ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "other";
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool Remind7Days { get; set; }
    public bool Remind3Days { get; set; }
    public bool Remind2Days { get; set; }
    public bool Remind24Hours { get; set; }
    /// <summary>True for assignment due dates synthesized from the Assignments table.</summary>
    public bool IsSynthesized { get; set; }
}

public class CourseEventCreateRequest
{
    [Required]
    public int CourseId { get; set; }

    public int? ModuleId { get; set; }

    /// <summary>Link to an existing assignment (EventType must be "assignment").</summary>
    public int? AssignmentId { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Event type is required")]
    public string EventType { get; set; } = "other";

    [Required]
    public DateTime StartsAtUtc { get; set; }

    public DateTime? EndsAtUtc { get; set; }

    public bool Remind7Days { get; set; }
    public bool Remind3Days { get; set; }
    public bool Remind2Days { get; set; }
    public bool Remind24Hours { get; set; }
}

public class CourseEventUpdateRequest
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Event type is required")]
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
