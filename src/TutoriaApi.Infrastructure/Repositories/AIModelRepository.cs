using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class AIModelRepository : Repository<AIModel>, IAIModelRepository
{
    public AIModelRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<AIModel?> GetWithModulesAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Modules)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<AIModel>> GetActiveModelsAsync()
    {
        return await _dbSet
            .Where(a => a.IsActive && !a.IsDeprecated)
            .OrderBy(a => a.Provider)
            .ThenBy(a => a.DisplayName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AIModel>> GetByProviderAsync(string provider)
    {
        return await _dbSet
            .Where(a => a.Provider == provider)
            .OrderBy(a => a.DisplayName)
            .ToListAsync();
    }

    public async Task<AIModel?> GetByModelNameAsync(string modelName)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.ModelName == modelName);
    }

    public async Task<bool> ExistsByModelNameAsync(string modelName)
    {
        return await _dbSet.AnyAsync(a => a.ModelName == modelName);
    }
}
