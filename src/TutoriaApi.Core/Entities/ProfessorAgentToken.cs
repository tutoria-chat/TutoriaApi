namespace TutoriaApi.Core.Entities;

public class ProfessorAgentToken : BaseEntity
{
    public required string Token { get; set; }
    public int ProfessorAgentId { get; set; }
    public int ProfessorId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool AllowChat { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public ProfessorAgent ProfessorAgent { get; set; } = null!;
    public User Professor { get; set; } = null!;
}
