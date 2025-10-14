namespace TutoriaApi.Core.Entities;

public class ModuleAccessToken : BaseEntity
{
    public required string Token { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int ModuleId { get; set; }
    public int? CreatedByProfessorId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    // Permissions
    public bool AllowChat { get; set; } = true;
    public bool AllowFileAccess { get; set; } = true;

    // Usage tracking
    public int UsageCount { get; set; } = 0;
    public DateTime? LastUsedAt { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
    public User? CreatedBy { get; set; }
}
