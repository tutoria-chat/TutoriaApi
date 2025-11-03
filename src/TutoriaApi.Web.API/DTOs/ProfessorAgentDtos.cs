using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class ProfessorAgentListDto
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public string? ProfessorName { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TutorLanguage { get; set; } = "pt-br";
    public int? AIModelId { get; set; }
    public string? AIModelDisplayName { get; set; }
    public bool IsActive { get; set; }
    public int TokensCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProfessorAgentDetailDto
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public string? ProfessorName { get; set; }
    public string? ProfessorEmail { get; set; }
    public int UniversityId { get; set; }
    public UniversityDto? University { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? OpenAIAssistantId { get; set; }
    public string? OpenAIVectorStoreId { get; set; }
    public string TutorLanguage { get; set; } = "pt-br";
    public int? AIModelId { get; set; }
    public AIModelListDto? AIModel { get; set; }
    public bool IsActive { get; set; }
    public List<ProfessorAgentTokenListDto> Tokens { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProfessorAgentCreateRequest
{
    [Required(ErrorMessage = "Professor ID is required")]
    public int ProfessorId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [MaxLength(10)]
    public string? TutorLanguage { get; set; }

    public int? AIModelId { get; set; }
}

public class ProfessorAgentUpdateRequest
{
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [MaxLength(10)]
    public string? TutorLanguage { get; set; }

    public int? AIModelId { get; set; }

    public bool? IsActive { get; set; }
}

public class ProfessorAgentTokenListDto
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ProfessorAgentId { get; set; }
    public int ProfessorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool AllowChat { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ProfessorAgentTokenDetailDto
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ProfessorAgentId { get; set; }
    public ProfessorAgentListDto? ProfessorAgent { get; set; }
    public int ProfessorId { get; set; }
    public string? ProfessorName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool AllowChat { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProfessorAgentTokenCreateRequest
{
    [Required(ErrorMessage = "Professor Agent ID is required")]
    public int ProfessorAgentId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    public bool AllowChat { get; set; } = true;

    public DateTime? ExpiresAt { get; set; }
}

public class ProfessorAgentTokenUpdateRequest
{
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    public bool? AllowChat { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// DTO for listing professors with their agent status
/// </summary>
public class ProfessorAgentStatusDto
{
    public int ProfessorId { get; set; }
    public string ProfessorName { get; set; } = string.Empty;
    public string ProfessorEmail { get; set; } = string.Empty;
    public bool HasAgent { get; set; }
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
    public bool? AgentIsActive { get; set; }
    public DateTime? AgentCreatedAt { get; set; }
}
