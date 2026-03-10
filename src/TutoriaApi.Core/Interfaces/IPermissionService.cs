using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IPermissionService
{
    Task<List<Permission>> GetAllPermissionsAsync();
    Task<List<Permission>> GetRolePermissionsAsync(string role);
    Task<List<Permission>> GetUserEffectivePermissionsAsync(int userId, string role);
    Task<List<UserPermission>> GetUserExtraPermissionsAsync(int userId);
    Task SetUserExtraPermissionsAsync(int userId, List<int> permissionIds, int grantedByUserId);

    // CRUD for permission definitions (super admin only)
    Task<Permission> CreatePermissionAsync(Permission permission);
    Task<Permission> UpdatePermissionAsync(int id, Permission permission);
    Task DeletePermissionAsync(int id);
}
