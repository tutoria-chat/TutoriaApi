using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProfessorAgentTokenRepository : IRepository<ProfessorAgentToken>
{
    Task<ProfessorAgentToken?> GetByTokenAsync(string token);
    Task<IEnumerable<ProfessorAgentToken>> GetByProfessorAgentIdAsync(int professorAgentId);
    Task<IEnumerable<ProfessorAgentToken>> GetByProfessorIdAsync(int professorId);
    Task<bool> IsTokenValidAsync(string token);
    Task<ProfessorAgentToken?> GetByTokenWithDetailsAsync(string token);
}
