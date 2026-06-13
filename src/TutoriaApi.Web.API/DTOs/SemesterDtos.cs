using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class SemesterDto
{
    public int Id { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool ChampionsAwarded { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SemesterCreateRequest
{
    [Required]
    public int UniversityId { get; set; }

    [Required(ErrorMessage = "Label is required")]
    [MaxLength(50)]
    public string Label { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAtUtc { get; set; }

    [Required]
    public DateTime EndsAtUtc { get; set; }
}

public class SemesterUpdateRequest
{
    [MaxLength(50)]
    public string? Label { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
}
