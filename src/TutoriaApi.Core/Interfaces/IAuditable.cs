namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Interface for entities that support automatic audit tracking (CreatedAt/UpdatedAt).
/// Use this for entities that do NOT have database triggers.
/// DbContext.SaveChanges will automatically update these properties.
/// </summary>
public interface IAuditable
{
    DateTime? CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
