using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Provides access to the currently authenticated user's information throughout the request pipeline.
/// This service is scoped per HTTP request and extracts user data from JWT claims.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current authenticated user with claims data populated.
    /// </summary>
    /// <returns>User entity with data from JWT claims</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when no authenticated user is found</exception>
    User GetCurrentUser();

    /// <summary>
    /// Gets the current user's ID.
    /// </summary>
    /// <returns>User ID from JWT claims</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when no authenticated user is found</exception>
    int GetUserId();

    /// <summary>
    /// Gets the current user's username.
    /// </summary>
    /// <returns>Username from JWT claims</returns>
    string GetUsername();

    /// <summary>
    /// Gets the current user's type (super_admin, professor, manager, etc.).
    /// </summary>
    /// <returns>User type from JWT claims</returns>
    string GetUserType();

    /// <summary>
    /// Gets the current user's university ID (if applicable).
    /// </summary>
    /// <returns>University ID or null if not associated with a university</returns>
    int? GetUniversityId();

    /// <summary>
    /// Checks if the current user is a super admin.
    /// </summary>
    /// <returns>True if super admin, false otherwise</returns>
    bool IsSuperAdmin();

    /// <summary>
    /// Checks if the current user has admin privileges (SuperAdmin or Manager).
    /// </summary>
    /// <returns>True if has admin privileges, false otherwise</returns>
    bool HasAdminPrivileges();

    /// <summary>
    /// Checks if a user is currently authenticated.
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    bool IsAuthenticated();
}
