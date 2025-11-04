using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProfessorAgentRepository : IRepository<ProfessorAgent>
{
    /// <summary>
    /// Gets active agent for a professor
    /// </summary>
    Task<ProfessorAgent?> GetByProfessorIdAsync(int professorId);

    /// <summary>
    /// Gets ANY agent (active or inactive) for a professor - used to check existence
    /// </summary>
    Task<ProfessorAgent?> GetByProfessorIdIncludingInactiveAsync(int professorId);

    Task<ProfessorAgent?> GetWithDetailsAsync(int id);
    Task<IEnumerable<ProfessorAgent>> GetByUniversityIdAsync(int universityId);
    Task<bool> ExistsByProfessorIdAsync(int professorId);
    Task<IEnumerable<ProfessorAgent>> GetActiveAgentsAsync();
}
