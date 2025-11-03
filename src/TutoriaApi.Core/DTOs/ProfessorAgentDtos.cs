namespace TutoriaApi.Core.DTOs;

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
