using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class QuizUploadJobRepository : Repository<QuizUploadJob>, IQuizUploadJobRepository
{
    public QuizUploadJobRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<QuizUploadJob>> GetByModuleIdAsync(int moduleId)
    {
        return await _dbSet
            .Include(q => q.Module)
            .Where(q => q.ModuleId == moduleId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<QuizUploadJob>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Include(q => q.Module)
            .Where(q => q.Status == status)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<QuizUploadJob?> GetWithModuleAsync(int id)
    {
        return await _dbSet
            .Include(q => q.Module)
            .FirstOrDefaultAsync(q => q.Id == id);
    }
}
