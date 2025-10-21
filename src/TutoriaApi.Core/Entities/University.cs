namespace TutoriaApi.Core.Entities;

public class University : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; } // Fantasy Name (Nome Fantasia) - e.g., USP, BYU
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; } // CNPJ in Brazil, Tax ID in other countries
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Website { get; set; }

    // Subscription tier (1 = Basic, 2 = Standard, 3 = Premium)
    public int SubscriptionTier { get; set; } = 3;

    // Navigation properties
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Professor> Professors { get; set; } = new List<Professor>();
}
