using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages permissions for the claims-based authorization system.
/// </summary>
/// <remarks>
/// Provides endpoints for:
/// - Listing all available permissions
/// - Getting default permissions for a role
/// - Getting effective permissions for a user (role + extra)
/// - Getting/setting user-specific extra permissions
///
/// **Authorization**:
/// - GET /api/permissions: Authenticated users
/// - GET /api/permissions/roles/{role}: Authenticated users
/// - User-specific endpoints: AdminOrAbove policy
/// </remarks>
[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        IPermissionService permissionService,
        ICurrentUserService currentUserService,
        ILogger<PermissionsController> logger)
    {
        _permissionService = permissionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available permissions in the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PermissionDto>>> GetAll()
    {
        try
        {
            var permissions = await _permissionService.GetAllPermissionsAsync();
            return Ok(permissions.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all permissions");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get default permissions for a specific role.
    /// </summary>
    [HttpGet("roles/{role}")]
    [ProducesResponseType(typeof(List<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PermissionDto>>> GetByRole(string role)
    {
        try
        {
            var permissions = await _permissionService.GetRolePermissionsAsync(role);
            return Ok(permissions.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for role {Role}", role);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get effective permissions for a user (role defaults + extra permissions).
    /// Returns a list of permission codes.
    /// </summary>
    [HttpGet("users/{userId}")]
    [Authorize(Policy = "AdminOrAbove")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetUserEffective(int userId)
    {
        try
        {
            // We need the user's role to compute effective permissions.
            // Get it from the user record via the user repository is not available here,
            // so we query the UserPermissions and RolePermissions through the service.
            // The caller must know the user's role — but for simplicity, we look up the user type.
            var user = await GetUserTypeForId(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var permissions = await _permissionService.GetUserEffectivePermissionsAsync(userId, user);
            var codes = permissions.Select(p => p.Code).ToList();

            return Ok(codes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting effective permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get only the extra (user-specific) permissions for a user.
    /// </summary>
    [HttpGet("users/{userId}/extra")]
    [Authorize(Policy = "AdminOrAbove")]
    [ProducesResponseType(typeof(List<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PermissionDto>>> GetUserExtra(int userId)
    {
        try
        {
            var userPermissions = await _permissionService.GetUserExtraPermissionsAsync(userId);
            var dtos = userPermissions
                .Where(up => up.Permission != null)
                .Select(up => MapToDto(up.Permission!))
                .ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting extra permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Set the extra (user-specific) permissions for a user.
    /// Replaces all existing extra permissions with the provided list.
    ///
    /// Security: Prevents privilege escalation by ensuring the caller can only
    /// assign permissions they themselves possess. Also blocks self-assignment
    /// to prevent users from granting themselves elevated access.
    /// </summary>
    [HttpPut("users/{userId}")]
    [Authorize(Policy = "AdminOrAbove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> SetUserExtra(int userId, [FromBody] SetUserPermissionsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var grantedByUserId = _currentUserService.GetUserId();
            var grantedByRole = _currentUserService.GetUserType();

            // SECURITY: Block self-assignment to prevent privilege escalation
            if (userId == grantedByUserId)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to modify their own permissions (blocked)",
                    grantedByUserId);
                return StatusCode(403, new { message = "You cannot modify your own permissions." });
            }

            // SECURITY: Verify the caller can only grant permissions they themselves have.
            // This prevents a manager from assigning super-admin-level permissions (e.g., universities:read)
            // to another user (or creating a new user with escalated access).
            if (request.PermissionIds.Count > 0)
            {
                var callerPermissions = await _permissionService.GetUserEffectivePermissionsAsync(
                    grantedByUserId, grantedByRole);
                var callerPermissionIds = new HashSet<int>(callerPermissions.Select(p => p.Id));

                var unauthorizedIds = request.PermissionIds
                    .Where(id => !callerPermissionIds.Contains(id))
                    .ToList();

                if (unauthorizedIds.Count > 0)
                {
                    _logger.LogWarning(
                        "User {UserId} (role: {Role}) attempted to assign permissions they don't have: [{PermIds}]",
                        grantedByUserId, grantedByRole, string.Join(", ", unauthorizedIds));
                    return StatusCode(403, new { message = "You can only assign permissions that you yourself possess." });
                }
            }

            await _permissionService.SetUserExtraPermissionsAsync(userId, request.PermissionIds, grantedByUserId);

            _logger.LogInformation(
                "User {GrantedBy} set {Count} extra permissions for user {UserId}",
                grantedByUserId, request.PermissionIds.Count, userId);

            return Ok(new { message = "User permissions updated successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting extra permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new permission definition. Super admin only.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionDto>> Create([FromBody] CreatePermissionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Resource) ||
            string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Code, resource, action, and category are required." });
        }

        try
        {
            var permission = new TutoriaApi.Core.Entities.Permission
            {
                Code = request.Code.Trim(),
                Resource = request.Resource.Trim(),
                Action = request.Action.Trim(),
                Scope = request.Scope?.Trim() ?? "university",
                Category = request.Category.Trim(),
                Description = request.Description?.Trim(),
                DisplayOrder = request.DisplayOrder
            };

            var created = await _permissionService.CreatePermissionAsync(permission);

            var dto = MapToDto(created);
            return CreatedAtAction(nameof(GetAll), dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing permission definition. Super admin only.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionDto>> Update(int id, [FromBody] CreatePermissionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Resource) ||
            string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Code, resource, action, and category are required." });
        }

        try
        {
            var permission = new TutoriaApi.Core.Entities.Permission
            {
                Code = request.Code.Trim(),
                Resource = request.Resource.Trim(),
                Action = request.Action.Trim(),
                Scope = request.Scope?.Trim() ?? "university",
                Category = request.Category.Trim(),
                Description = request.Description?.Trim(),
                DisplayOrder = request.DisplayOrder
            };

            var updated = await _permissionService.UpdatePermissionAsync(id, permission);
            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permission {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a permission definition. Super admin only.
    /// Also removes it from all role and user assignments.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _permissionService.DeletePermissionAsync(id);
            return Ok(new { message = "Permission deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static PermissionDto MapToDto(TutoriaApi.Core.Entities.Permission p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Resource = p.Resource,
        Action = p.Action,
        Scope = p.Scope,
        Category = p.Category,
        Description = p.Description,
        DisplayOrder = p.DisplayOrder
    };

    /// <summary>
    /// Helper to look up a user's UserType by their UserId.
    /// Uses IUserService to avoid direct DbContext usage in the controller.
    /// </summary>
    private async Task<string?> GetUserTypeForId(int userId)
    {
        // Use the IUserService to get user info
        var userService = HttpContext.RequestServices.GetService<IUserService>();
        if (userService == null) return null;

        var viewModel = await userService.GetByIdAsync(userId);
        return viewModel?.User.UserType;
    }
}

// DTOs for the Permissions API

public class PermissionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class SetUserPermissionsRequest
{
    public List<int> PermissionIds { get; set; } = new();
}

public class CreatePermissionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}
