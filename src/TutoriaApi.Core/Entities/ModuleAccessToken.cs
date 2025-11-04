namespace TutoriaApi.Core.Entities;

/// <summary>
/// Module access tokens are API keys that grant access to the AI chat widget for a specific module.
/// ARCHITECTURE: These tokens work like GitHub Personal Access Tokens (PATs) - they are MEANT to be visible
/// and shareable. They are NOT sensitive credentials like passwords.
///
/// SECURITY MODEL:
/// - Tokens are displayed in plaintext to professors who create them
/// - Professors share tokens with students via LMS, email, or widget embeds
/// - Tokens are scoped to a single module with specific permissions (chat, file access)
/// - Tokens can expire and be revoked at any time
/// - Tokens are rate-limited and monitored for abuse
///
/// WHY PLAINTEXT STORAGE IS ACCEPTABLE:
/// - These are authorization tokens, not authentication credentials
/// - They grant access to AI tutoring features, not sensitive user data
/// - Similar to API keys in cloud services (AWS Access Keys, OpenAI API keys)
/// - Professors NEED to see and copy tokens to distribute to students
/// - Compromised tokens can be immediately revoked without user impact
///
/// COMPARISON TO GITHUB PERSONAL ACCESS TOKENS:
/// - GitHub shows PATs in plaintext when created, stores them hashed
/// - We show tokens in plaintext when created AND after creation (professors need to retrieve them)
/// - Trade-off: Better UX for professors vs slightly reduced security (acceptable for this use case)
///
/// FUTURE ENHANCEMENT: Consider implementing "view once" tokens where:
/// 1. Token shown in plaintext immediately after creation
/// 2. Token hashed in database (SHA-256)
/// 3. Only first few characters shown in UI after creation ("tutoria_abc123...")
/// 4. Professors must copy token during creation (cannot retrieve later)
/// </summary>
public class ModuleAccessToken : BaseEntity
{
    /// <summary>
    /// The actual token string (64-character secure random string).
    /// ARCHITECTURE NOTE: Stored in plaintext so professors can retrieve and share it.
    /// This is intentional and matches the design pattern of API keys that users need to view.
    /// Not hashed because: 1) Professors need to retrieve tokens, 2) Tokens are revocable,
    /// 3) They grant limited scope (module-level access only), 4) Similar to cloud API keys.
    /// </summary>
    public required string Token { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int ModuleId { get; set; }
    public int? CreatedByProfessorId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    // Permissions
    public bool AllowChat { get; set; } = true;
    public bool AllowFileAccess { get; set; } = true;

    // Usage tracking
    public int UsageCount { get; set; } = 0;
    public DateTime? LastUsedAt { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
    public User? CreatedBy { get; set; }
}
