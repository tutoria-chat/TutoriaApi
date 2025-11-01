namespace TutoriaApi.Core.Entities;

public class ProfessorAgent : BaseEntity
{
    public int ProfessorId { get; set; }
    public int UniversityId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? OpenAIAssistantId { get; set; }
    public string? OpenAIVectorStoreId { get; set; }
    public string TutorLanguage { get; set; } = "pt-br";
    public int? AIModelId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User Professor { get; set; } = null!;
    public University University { get; set; } = null!;
    public AIModel? AIModel { get; set; }
    public ICollection<ProfessorAgentToken> ProfessorAgentTokens { get; set; } = new List<ProfessorAgentToken>();
}
