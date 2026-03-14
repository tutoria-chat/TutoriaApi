using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// View model for a user's university membership, including university summary details.
/// </summary>
public class UserUniversitySummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// View model for user search results, including their university memberships.
/// </summary>
public class UserSearchViewModel
{
    public User User { get; set; } = null!;
    public List<UserUniversitySummary> Universities { get; set; } = new();
}

/// <summary>
/// Service for managing user-university multi-tenancy relationships.
/// </summary>
public interface IUserUniversityService
{
    /// <summary>
    /// Get all universities a user belongs to, with summary details.
    /// </summary>
    Task<List<UserUniversitySummary>> GetUserUniversitiesAsync(int userId);

    /// <summary>
    /// Add a user to a university. Validates authorization and university existence.
    /// If the user has no active university (UniversityId is null), sets it.
    /// </summary>
    Task AddUserToUniversityAsync(int userId, int universityId, User currentUser);

    /// <summary>
    /// Remove a user from a university. Validates authorization.
    /// If removing the active university, switches to another (or null).
    /// </summary>
    Task RemoveUserFromUniversityAsync(int userId, int universityId, User currentUser);

    /// <summary>
    /// Switch the user's active university (Users.UniversityId).
    /// Validates the user belongs to the target university.
    /// </summary>
    Task<User> SwitchUniversityAsync(int userId, int universityId);

    /// <summary>
    /// Search users by email or name, optionally excluding users already in a specific university.
    /// Returns paginated results with their university memberships.
    /// </summary>
    Task<(List<UserSearchViewModel> Items, int Total)> SearchUsersAsync(
        string query,
        int? excludeUniversityId,
        int page,
        int pageSize);
}
