namespace TutoriaApi.Core.Entities;

public class University : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; } // Fantasy Name (Nome Fantasia) - e.g., USP, BYU
    public string? Description { get; set; }

    // Address fields
    public string? Address { get; set; } // Deprecated: Use individual address fields below
    public string? PostalCode { get; set; } // Postal code (ZIP in US, CEP in Brazil, etc.)
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }

    public string? TaxId { get; set; } // CNPJ in Brazil, Tax ID in other countries
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Website { get; set; }

    // Subscription tier (1 = Basic, 2 = Standard, 3 = Premium)
    public int SubscriptionTier { get; set; } = 3;

    // Stripe & enterprise fields
    public string? StripeCustomerId { get; set; }
    public bool IsEnterprise { get; set; }
    public bool HasAssignments { get; set; }
    public bool HasAIQuizzes { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? MaxCourses { get; set; }
    public int? MaxModules { get; set; }
    public int? MaxStudents { get; set; }

    /// <summary>
    /// Trusted web origins (scheme://host[:port], one per line) that may call the API
    /// from a browser — the institution's LMS/Moodle etc. Powers the dynamic CORS
    /// allowlist (see ITrustedOriginsProvider). Null/empty = none beyond our defaults.
    /// </summary>
    public string? AllowedOrigins { get; set; }

    // Navigation properties
    public UniversityPersonalization? Personalization { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Professor> Professors { get; set; } = new List<Professor>();
}
