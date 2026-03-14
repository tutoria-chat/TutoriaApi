namespace TutoriaApi.Core.Entities;

/// <summary>
/// Join table for many-to-many relationship between Users and Universities.
/// Enables multi-tenancy: a user can belong to multiple universities.
/// Raw POCO - no navigation properties to avoid EF Core issues.
/// </summary>
public class UserUniversity
{
    public int UserId { get; set; }
    public int UniversityId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
