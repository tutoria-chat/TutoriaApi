namespace TutoriaApi.Core.Entities;

/// <summary>Expo push token of one of a user's devices.</summary>
public class DeviceToken : BaseEntity
{
    public int UserId { get; set; }
    public required string Token { get; set; }
    /// <summary>ios | android</summary>
    public string? Platform { get; set; }
}
