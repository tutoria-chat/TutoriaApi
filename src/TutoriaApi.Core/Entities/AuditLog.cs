namespace TutoriaApi.Core.Entities;

public class AuditLog : BaseEntity
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? UniversityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Changes { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public University? University { get; set; }
}
