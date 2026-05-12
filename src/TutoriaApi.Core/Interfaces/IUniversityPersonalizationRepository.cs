using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityPersonalizationRepository : IRepository<UniversityPersonalization>
{
    Task<UniversityPersonalization?> GetByUniversityIdAsync(int universityId);
}
