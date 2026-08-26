using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Repository for managing User-University junction table relationships.
/// Supports multi-tenancy: users can belong to multiple universities.
/// </summary>
public interface IUserUniversityRepository
{
    /// <summary>
    /// Get all university IDs a user belongs to
    /// </summary>
    Task<List<int>> GetUniversityIdsByUserIdAsync(int userId);

    /// <summary>
    /// Get all user IDs belonging to a university
    /// </summary>
    Task<List<int>> GetUserIdsByUniversityIdAsync(int universityId);

    /// <summary>
    /// Get all UserUniversity entries for a user (includes university details via join)
    /// </summary>
    Task<List<UserUniversity>> GetByUserIdAsync(int userId);

    /// <summary>
    /// Check if a user belongs to a specific university
    /// </summary>
    Task<bool> ExistsAsync(int userId, int universityId);

    /// <summary>
    /// Add a user to a university (no-op if already exists)
    /// </summary>
    Task AddAsync(int userId, int universityId);
    Task AddAsync(int userId, int universityId, string? externalId);
    /// <summary>Set the matricula (ExternalId) on an existing membership row; no-op if none.</summary>
    Task SetExternalIdAsync(int userId, int universityId, string? externalId);

    /// <summary>
    /// Remove a user from a university
    /// </summary>
    Task RemoveAsync(int userId, int universityId);

    /// <summary>
    /// Remove all university memberships for a user
    /// </summary>
    Task RemoveAllForUserAsync(int userId);

    /// <summary>
    /// Remove all user memberships for a university
    /// </summary>
    Task RemoveAllForUniversityAsync(int universityId);

    /// <summary>
    /// Get count of universities a user belongs to
    /// </summary>
    Task<int> GetUniversityCountByUserIdAsync(int userId);

    /// <summary>
    /// Get all UserUniversity entries for multiple users in a single query (batch fetch).
    /// </summary>
    Task<List<UserUniversity>> GetByUserIdsAsync(List<int> userIds);
}
