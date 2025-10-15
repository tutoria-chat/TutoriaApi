using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IProfessorRepository : IRepository<Professor>
{
    Task<Professor?> GetByUsernameAsync(string username);
    Task<Professor?> GetByEmailAsync(string email);
    Task<IEnumerable<Professor>> GetByUniversityIdAsync(int universityId);
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email);
}
