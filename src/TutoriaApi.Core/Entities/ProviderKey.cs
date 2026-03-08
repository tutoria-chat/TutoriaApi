namespace TutoriaApi.Core.Entities;

public class ProviderKey : BaseEntity
{
    public required string Provider { get; set; }
    public required string KeyName { get; set; }
    public required string EncryptedApiKey { get; set; }
    public string? Region { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime? CooldownUntil { get; set; }
}
