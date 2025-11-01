using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ProfessorAgentTokenRepository : Repository<ProfessorAgentToken>, IProfessorAgentTokenRepository
{
    public ProfessorAgentTokenRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<ProfessorAgentToken?> GetByTokenAsync(string token)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pat => pat.Token == token);
    }

    public async Task<IEnumerable<ProfessorAgentToken>> GetByProfessorAgentIdAsync(int professorAgentId)
    {
        return await _dbSet
            .Where(pat => pat.ProfessorAgentId == professorAgentId)
            .OrderByDescending(pat => pat.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProfessorAgentToken>> GetByProfessorIdAsync(int professorId)
    {
        return await _dbSet
            .Include(pat => pat.ProfessorAgent)
            .Where(pat => pat.ProfessorId == professorId)
            .OrderByDescending(pat => pat.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        var tokenEntity = await _dbSet
            .FirstOrDefaultAsync(pat => pat.Token == token);

        if (tokenEntity == null) return false;

        // Check if token is expired
        if (tokenEntity.ExpiresAt.HasValue && tokenEntity.ExpiresAt.Value < DateTime.UtcNow)
            return false;

        // Check if chat is allowed
        if (!tokenEntity.AllowChat)
            return false;

        return true;
    }

    public async Task<ProfessorAgentToken?> GetByTokenWithDetailsAsync(string token)
    {
        return await _dbSet
            .Include(pat => pat.ProfessorAgent)
                .ThenInclude(pa => pa.Professor)
            .Include(pat => pat.ProfessorAgent)
                .ThenInclude(pa => pa.University)
            .Include(pat => pat.ProfessorAgent)
                .ThenInclude(pa => pa.AIModel)
            .FirstOrDefaultAsync(pat => pat.Token == token);
    }
}
