using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProfessorAgentRepository : IRepository<ProfessorAgent>
{
    Task<ProfessorAgent?> GetByProfessorIdAsync(int professorId);
    Task<ProfessorAgent?> GetWithDetailsAsync(int id);
    Task<IEnumerable<ProfessorAgent>> GetByUniversityIdAsync(int universityId);
    Task<bool> ExistsByProfessorIdAsync(int professorId);
    Task<IEnumerable<ProfessorAgent>> GetActiveAgentsAsync();
}
