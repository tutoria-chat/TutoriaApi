using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ProfessorAgentRepository : Repository<ProfessorAgent>, IProfessorAgentRepository
{
    public ProfessorAgentRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<ProfessorAgent?> GetByProfessorIdAsync(int professorId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pa => pa.ProfessorId == professorId && pa.IsActive);
    }

    public async Task<ProfessorAgent?> GetByProfessorIdIncludingInactiveAsync(int professorId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pa => pa.ProfessorId == professorId);
    }

    public async Task<ProfessorAgent?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(pa => pa.Professor)
            .Include(pa => pa.University)
            .Include(pa => pa.AIModel)
            .Include(pa => pa.ProfessorAgentTokens)
            .FirstOrDefaultAsync(pa => pa.Id == id);
    }

    public async Task<IEnumerable<ProfessorAgent>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(pa => pa.Professor)
            .Include(pa => pa.AIModel)
            .Where(pa => pa.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<bool> ExistsByProfessorIdAsync(int professorId)
    {
        return await _dbSet
            .AnyAsync(pa => pa.ProfessorId == professorId && pa.IsActive);
    }

    public async Task<IEnumerable<ProfessorAgent>> GetActiveAgentsAsync()
    {
        return await _dbSet
            .Include(pa => pa.Professor)
            .Include(pa => pa.University)
            .Include(pa => pa.AIModel)
            .Where(pa => pa.IsActive)
            .ToListAsync();
    }
}
