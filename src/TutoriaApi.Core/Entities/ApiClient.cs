namespace TutoriaApi.Core.Entities;

public class ApiClient : BaseEntity
{
    public string ClientId { get; set; } = string.Empty;
    public string HashedSecret { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string Scopes { get; set; } = string.Empty; // JSON array stored as string
    public DateTime? LastUsedAt { get; set; }
}
