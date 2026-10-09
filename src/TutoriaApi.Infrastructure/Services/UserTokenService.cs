using System.Security.Claims;
using System.Text.Json;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UserTokenService : IUserTokenService
{
    public const int AccessTokenMinutes = 480; // 8 hours

    private readonly IJwtService _jwtService;
    private readonly IPermissionService _permissionService;

    public UserTokenService(IJwtService jwtService, IPermissionService permissionService)
    {
        _jwtService = jwtService;
        _permissionService = permissionService;
    }

    public string[] ScopesFor(User user) => user.UserType switch
    {
        "super_admin" => new[] { "api.read", "api.write", "api.admin" },
        "manager" => new[] { "api.read", "api.write", "api.manage" },
        "tutor" => new[] { "api.read", "api.write" },
        "platform_coordinator" => new[] { "api.read", "api.write" },
        "professor" when user.IsAdmin == true => new[] { "api.read", "api.write", "api.manage" },
        "professor" => new[] { "api.read", "api.write" },
        "student" => new[] { "api.read" },
        _ => Array.Empty<string>()
    };

    public async Task<Dictionary<string, string>> BuildClaimsAsync(User user)
    {
        var claims = new Dictionary<string, string>
        {
            // Simple-named claims for cross-platform JWT decoding (tutoria-app, widget)
            ["user_id"] = user.UserId.ToString(),
            ["username"] = user.Username,
            ["user_type"] = user.UserType,
        };

        // Name claims — both simple and .NET standard
        if (!string.IsNullOrEmpty(user.FirstName))
        {
            claims["first_name"] = user.FirstName;
            claims[ClaimTypes.GivenName] = user.FirstName;
        }
        if (!string.IsNullOrEmpty(user.LastName))
        {
            claims["last_name"] = user.LastName;
            claims[ClaimTypes.Surname] = user.LastName;
        }
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims[ClaimTypes.Email] = user.Email;
        }

        // Role-specific claims
        if (user.IsAdmin.HasValue)
        {
            claims["isAdmin"] = user.IsAdmin.Value.ToString().ToLower();
        }
        if (user.UniversityId.HasValue)
        {
            claims["UniversityId"] = user.UniversityId.Value.ToString();
        }

        // Permissions claim — effective permissions (role defaults + user extras)
        var effectivePermissions = await _permissionService.GetUserEffectivePermissionsAsync(user.UserId, user.UserType);
        claims["permissions"] = JsonSerializer.Serialize(
            effectivePermissions.Select(p => p.Code).ToList()
        );

        return claims;
    }

    public async Task<(string AccessToken, string RefreshToken)> IssueAsync(User user)
    {
        var scopes = ScopesFor(user);
        var claims = await BuildClaimsAsync(user);
        var access = _jwtService.GenerateToken(user.UserId.ToString(), user.UserType, scopes, AccessTokenMinutes, claims);
        var refresh = _jwtService.GenerateRefreshToken(user.UserId.ToString(), user.UserType, scopes, claims);
        return (access, refresh);
    }
}
