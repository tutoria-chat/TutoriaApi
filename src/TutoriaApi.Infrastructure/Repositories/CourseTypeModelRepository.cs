using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CourseTypeModelRepository : Repository<CourseTypeModel>, ICourseTypeModelRepository
{
    public CourseTypeModelRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CourseTypeModel>> GetByCourseTypeAsync(string courseType)
    {
        return await _dbSet
            .Include(ctm => ctm.AIModel)
            .Where(ctm => ctm.CourseType == courseType)
            .OrderBy(ctm => ctm.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<CourseTypeModel>> GetActiveAsync()
    {
        return await _dbSet
            .Include(ctm => ctm.AIModel)
            .Where(ctm => ctm.IsActive)
            .OrderBy(ctm => ctm.CourseType)
            .ThenBy(ctm => ctm.Priority)
            .ToListAsync();
    }

    public async Task<CourseTypeModel?> GetWithAIModelAsync(int id)
    {
        return await _dbSet
            .Include(ctm => ctm.AIModel)
            .FirstOrDefaultAsync(ctm => ctm.Id == id);
    }
}
