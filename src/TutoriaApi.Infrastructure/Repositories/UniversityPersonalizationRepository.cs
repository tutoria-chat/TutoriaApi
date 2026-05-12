using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UniversityPersonalizationRepository : Repository<UniversityPersonalization>, IUniversityPersonalizationRepository
{
    public UniversityPersonalizationRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<UniversityPersonalization?> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.UniversityId == universityId);
    }
}
