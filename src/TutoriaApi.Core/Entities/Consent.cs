namespace TutoriaApi.Core.Entities;

public class Consent : BaseEntity
{
    public int UserId { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
