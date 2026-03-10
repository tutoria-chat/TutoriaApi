namespace TutoriaApi.Core.Entities;

public class Permission
{
    public int Id { get; set; }
    public required string Code { get; set; }        // e.g. "courses:create"
    public required string Resource { get; set; }     // e.g. "course"
    public required string Action { get; set; }       // e.g. "create"
    public string Scope { get; set; } = "university"; // "global" | "university" | "course"
    public required string Category { get; set; }     // e.g. "Courses" (for UI grouping)
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
