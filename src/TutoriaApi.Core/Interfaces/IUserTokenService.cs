using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Builds a user's JWT claims and issues access/refresh tokens. Shared by
/// AuthController and the student app so every login yields identical tokens.
/// </summary>
public interface IUserTokenService
{
    string[] ScopesFor(User user);
    Task<Dictionary<string, string>> BuildClaimsAsync(User user);
    /// <summary>8h access token + 30-day refresh token.</summary>
    Task<(string AccessToken, string RefreshToken)> IssueAsync(User user);
}
