using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Provides access to the currently authenticated user's information.
/// This service is scoped per HTTP request and extracts user data from JWT claims.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private User? _cachedUser;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal GetClaimsPrincipal()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            throw new UnauthorizedAccessException("No authenticated user found");
        }
        return user;
    }

    public User GetCurrentUser()
    {
        // Cache the user for the duration of the request
        if (_cachedUser != null)
        {
            return _cachedUser;
        }

        var claimsPrincipal = GetClaimsPrincipal();

        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || userId == 0)
        {
            throw new UnauthorizedAccessException("Invalid user ID in claims");
        }

        _cachedUser = new User
        {
            UserId = userId,
            Username = claimsPrincipal.Identity?.Name ?? "unknown",
            Email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@example.com",
            FirstName = claimsPrincipal.FindFirst(ClaimTypes.GivenName)?.Value ?? "Unknown",
            LastName = claimsPrincipal.FindFirst(ClaimTypes.Surname)?.Value ?? "User",
            UserType = claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value ?? UserTypes.Professor,
            UniversityId = int.TryParse(claimsPrincipal.FindFirst("UniversityId")?.Value, out var uniId) ? uniId : null,
            IsAdmin = bool.TryParse(claimsPrincipal.FindFirst("isAdmin")?.Value, out var isAdmin) && isAdmin
        };

        return _cachedUser;
    }

    public int GetUserId()
    {
        var claimsPrincipal = GetClaimsPrincipal();
        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId) || userId == 0)
        {
            throw new UnauthorizedAccessException("Invalid user ID in claims");
        }

        return userId;
    }

    public string GetUsername()
    {
        var claimsPrincipal = GetClaimsPrincipal();
        return claimsPrincipal.Identity?.Name ?? "unknown";
    }

    public string GetUserType()
    {
        var claimsPrincipal = GetClaimsPrincipal();
        return claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value ?? UserTypes.Professor;
    }

    public int? GetUniversityId()
    {
        if (!IsAuthenticated())
        {
            return null;
        }

        var claimsPrincipal = _httpContextAccessor.HttpContext!.User;
        return int.TryParse(claimsPrincipal.FindFirst("UniversityId")?.Value, out var uniId) ? uniId : null;
    }

    public bool IsSuperAdmin()
    {
        if (!IsAuthenticated())
        {
            return false;
        }

        var claimsPrincipal = _httpContextAccessor.HttpContext!.User;
        return claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value == UserTypes.SuperAdmin;
    }

    public bool HasAdminPrivileges()
    {
        if (!IsAuthenticated())
        {
            return false;
        }

        var claimsPrincipal = _httpContextAccessor.HttpContext!.User;
        var userType = claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value;

        // Super Admin or Manager always have admin privileges
        if (userType == UserTypes.SuperAdmin || userType == UserTypes.Manager)
        {
            return true;
        }

        // Professors with isAdmin claim also have admin privileges
        if (userType == UserTypes.Professor)
        {
            var isAdminClaim = claimsPrincipal.FindFirst("isAdmin")?.Value;
            return bool.TryParse(isAdminClaim, out var isAdmin) && isAdmin;
        }

        return false;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }
}
