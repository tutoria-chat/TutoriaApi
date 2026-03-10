namespace TutoriaApi.Core.Entities;

public class RolePermission
{
    public int Id { get; set; }
    public required string Role { get; set; }   // matches UserType values
    public int PermissionId { get; set; }

    // Navigation property
    public Permission? Permission { get; set; }
}
