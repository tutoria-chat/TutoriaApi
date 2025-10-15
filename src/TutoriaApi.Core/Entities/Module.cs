namespace TutoriaApi.Core.Entities;

public class Module : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public required string SystemPrompt { get; set; }
    public int? Semester { get; set; }
    public int? Year { get; set; }
    public int CourseId { get; set; }
    public string? OpenAIAssistantId { get; set; }
    public string? OpenAIVectorStoreId { get; set; }
    public DateTime? LastPromptImprovedAt { get; set; }
    public int PromptImprovementCount { get; set; } = 0;
    public string TutorLanguage { get; set; } = "pt-br";
    public int? AIModelId { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public AIModel? AIModel { get; set; }
    public ICollection<File> Files { get; set; } = new List<File>();
    public ICollection<ModuleAccessToken> ModuleAccessTokens { get; set; } = new List<ModuleAccessToken>();
}
