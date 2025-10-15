using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ProfessorRepository : Repository<Professor>, IProfessorRepository
{
    public ProfessorRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<Professor?> GetByUsernameAsync(string username)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Username == username);
    }

    public async Task<Professor?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<IEnumerable<Professor>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Where(p => p.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<bool> ExistsByUsernameOrEmailAsync(string username, string email)
    {
        return await _dbSet.AnyAsync(p => p.Username == username || p.Email == email);
    }
}
