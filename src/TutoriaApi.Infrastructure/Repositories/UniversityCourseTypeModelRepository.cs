using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UniversityCourseTypeModelRepository : Repository<UniversityCourseTypeModel>, IUniversityCourseTypeModelRepository
{
    public UniversityCourseTypeModelRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<UniversityCourseTypeModel>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(uctm => uctm.AIModel)
            .Include(uctm => uctm.University)
            .Where(uctm => uctm.UniversityId == universityId)
            .OrderBy(uctm => uctm.CourseType)
            .ThenBy(uctm => uctm.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<UniversityCourseTypeModel>> GetByUniversityAndCourseTypeAsync(int universityId, string courseType)
    {
        return await _dbSet
            .Include(uctm => uctm.AIModel)
            .Where(uctm => uctm.UniversityId == universityId && uctm.CourseType == courseType)
            .OrderBy(uctm => uctm.Priority)
            .ToListAsync();
    }

    public async Task<UniversityCourseTypeModel?> GetWithNavigationsAsync(int id)
    {
        return await _dbSet
            .Include(uctm => uctm.AIModel)
            .Include(uctm => uctm.University)
            .FirstOrDefaultAsync(uctm => uctm.Id == id);
    }
}
