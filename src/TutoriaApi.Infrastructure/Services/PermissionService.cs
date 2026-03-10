using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly TutoriaDbContext _context;

    /// <summary>
    /// Permission codes that are referenced by authorization policies in Program.cs.
    /// These codes cannot be renamed or deleted — doing so would break auth enforcement.
    /// </summary>
    private static readonly HashSet<string> ProtectedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "universities:read",  // SuperAdminOnly policy
        "staff:create",       // AdminOrAbove policy
        "students:read",      // ProfessorOrAbove policy
        "analytics:read"      // AnalyticsAccess policy
    };

    public PermissionService(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        return await _context.Permissions
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Permission>> GetRolePermissionsAsync(string role)
    {
        return await _context.RolePermissions
            .Where(rp => rp.Role == role)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission!)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<Permission>> GetUserEffectivePermissionsAsync(int userId, string role)
    {
        // Get permission IDs from role-based permissions
        var rolePermissionIds = _context.RolePermissions
            .Where(rp => rp.Role == role)
            .Select(rp => rp.PermissionId);

        // Get permission IDs from user-specific extra permissions
        var userPermissionIds = _context.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.PermissionId);

        // Union both sets and get distinct Permission entities
        var allPermissionIds = rolePermissionIds.Union(userPermissionIds);

        return await _context.Permissions
            .Where(p => allPermissionIds.Contains(p.Id))
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<UserPermission>> GetUserExtraPermissionsAsync(int userId)
    {
        return await _context.UserPermissions
            .Where(up => up.UserId == userId)
            .Include(up => up.Permission)
            .ToListAsync();
    }

    public async Task SetUserExtraPermissionsAsync(int userId, List<int> permissionIds, int grantedByUserId)
    {
        // Deduplicate
        var distinctIds = permissionIds.Distinct().ToList();

        // Validate all permission IDs exist
        if (distinctIds.Count > 0)
        {
            var existingIds = await _context.Permissions
                .Where(p => distinctIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            var invalidIds = distinctIds.Except(existingIds).ToList();
            if (invalidIds.Any())
            {
                throw new ArgumentException($"Invalid permission IDs: {string.Join(", ", invalidIds)}");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var existingPermissions = await _context.UserPermissions
                .Where(up => up.UserId == userId)
                .ToListAsync();
            _context.UserPermissions.RemoveRange(existingPermissions);

            var newPermissions = distinctIds.Select(permissionId => new UserPermission
            {
                UserId = userId,
                PermissionId = permissionId,
                GrantedBy = grantedByUserId,
                GrantedAt = DateTime.UtcNow
            }).ToList();

            await _context.UserPermissions.AddRangeAsync(newPermissions);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Permission> CreatePermissionAsync(Permission permission)
    {
        // Check for duplicate code
        var exists = await _context.Permissions.AnyAsync(p => p.Code == permission.Code);
        if (exists)
        {
            throw new ArgumentException($"A permission with code '{permission.Code}' already exists.");
        }

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();
        return permission;
    }

    public async Task<Permission> UpdatePermissionAsync(int id, Permission permission)
    {
        var existing = await _context.Permissions.FindAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Permission with ID {id} not found.");
        }

        // Prevent renaming protected permission codes used by authorization policies
        if (ProtectedCodes.Contains(existing.Code) && !string.Equals(existing.Code, permission.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Permission code '{existing.Code}' is used by an authorization policy and cannot be renamed. " +
                $"You may update its description, category, or display order, but the code must remain '{existing.Code}'.");
        }

        // Check for duplicate code (excluding current)
        var codeExists = await _context.Permissions.AnyAsync(p => p.Code == permission.Code && p.Id != id);
        if (codeExists)
        {
            throw new ArgumentException($"A permission with code '{permission.Code}' already exists.");
        }

        existing.Code = permission.Code;
        existing.Resource = permission.Resource;
        existing.Action = permission.Action;
        existing.Scope = permission.Scope;
        existing.Category = permission.Category;
        existing.Description = permission.Description;
        existing.DisplayOrder = permission.DisplayOrder;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeletePermissionAsync(int id)
    {
        var existing = await _context.Permissions.FindAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Permission with ID {id} not found.");
        }

        // Prevent deletion of protected permission codes used by authorization policies
        if (ProtectedCodes.Contains(existing.Code))
        {
            throw new InvalidOperationException(
                $"Permission '{existing.Code}' is used by an authorization policy and cannot be deleted. " +
                $"Removing it would break authentication enforcement.");
        }

        // Remove associated role permissions and user permissions first
        var rolePerms = await _context.RolePermissions.Where(rp => rp.PermissionId == id).ToListAsync();
        var userPerms = await _context.UserPermissions.Where(up => up.PermissionId == id).ToListAsync();

        _context.RolePermissions.RemoveRange(rolePerms);
        _context.UserPermissions.RemoveRange(userPerms);
        _context.Permissions.Remove(existing);

        await _context.SaveChangesAsync();
    }
}
