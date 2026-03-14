namespace TutoriaApi.Core.Entities;

public class UserInvitation
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public required string Email { get; set; }
    public required string UserType { get; set; }
    public int? UniversityId { get; set; }
    public bool IsAdmin { get; set; } = false;
    public string LanguagePreference { get; set; } = "pt-br";
    public int InvitedByUserId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int? CreatedUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
