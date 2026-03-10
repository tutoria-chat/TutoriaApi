namespace TutoriaApi.Core.Entities;

public class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PermissionId { get; set; }
    public int? GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public Permission? Permission { get; set; }
    public User? GrantedByUser { get; set; }
}
