namespace TutoriaApi.Core.Entities;

/// <summary>
/// Professor agent tokens are API keys that grant professors access to their AI agents.
/// ARCHITECTURE: These tokens work like GitHub Personal Access Tokens (PATs) - they are MEANT to be visible
/// to the professor who owns them. They are NOT passwords or sensitive credentials.
///
/// SECURITY MODEL:
/// - Tokens are shown to professors in plaintext for easy copying/usage
/// - Tokens grant access to professor-specific AI agents and features
/// - Tokens are scoped to the professor's agents only (no cross-professor access)
/// - Tokens can be revoked at any time by the professor
/// - Similar to API keys in services like GitHub, GitLab, Stripe, AWS
///
/// WHY PLAINTEXT STORAGE IS ACCEPTABLE:
/// - These are authorization tokens for API access, not login credentials
/// - Professors NEED to view tokens to use them in API calls or integrations
/// - Tokens grant access to professor's own resources only (isolated scope)
/// - Compromised tokens can be immediately revoked and regenerated
/// - Similar security model to cloud provider API keys (AWS Access Keys, etc.)
///
/// FUTURE ENHANCEMENT: Consider implementing "view once" pattern:
/// 1. Token shown in plaintext immediately after creation
/// 2. Token hashed in database after creation
/// 3. Only partial token shown in UI ("prof_abc123...xyz789")
/// 4. Professor must copy token during creation (cannot retrieve full token later)
/// </summary>
public class ProfessorAgentToken : BaseEntity
{
    /// <summary>
    /// The actual token string used for API authentication.
    /// ARCHITECTURE NOTE: Stored in plaintext so professors can retrieve and use their tokens.
    /// This is intentional - tokens are API keys (like AWS Access Keys), not passwords.
    /// Professors need to see tokens to integrate with external tools or make API calls.
    /// Security is maintained through: token revocation, expiry, scope isolation, rate limiting.
    /// </summary>
    public required string Token { get; set; }
    public int ProfessorAgentId { get; set; }
    public int ProfessorId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool AllowChat { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public ProfessorAgent ProfessorAgent { get; set; } = null!;
    public User Professor { get; set; } = null!;
}
