using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProfessorAgentService
{
    Task<ProfessorAgent?> GetByProfessorIdAsync(int professorId);
    Task<IEnumerable<ProfessorAgent>> GetAllAgentsAsync(int? universityId = null);
    Task<ProfessorAgent> CreateAgentAsync(int professorId, string name, string? description, string? systemPrompt, string? tutorLanguage, int? aiModelId);
    Task<ProfessorAgent> UpdateAgentAsync(int id, string? name, string? description, string? systemPrompt, string? tutorLanguage, int? aiModelId, bool? isActive);
    Task DeleteAgentAsync(int id);
    Task<ProfessorAgentToken> CreateTokenAsync(int agentId, int currentUserId, string currentUserType, string name, string? description, bool allowChat, DateTime? expiresAt);
    Task<IEnumerable<ProfessorAgentToken>> GetTokensByAgentIdAsync(int agentId);
}
