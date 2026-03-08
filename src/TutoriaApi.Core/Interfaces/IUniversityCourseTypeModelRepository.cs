using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityCourseTypeModelRepository : IRepository<UniversityCourseTypeModel>
{
    Task<IEnumerable<UniversityCourseTypeModel>> GetByUniversityIdAsync(int universityId);
    Task<IEnumerable<UniversityCourseTypeModel>> GetByUniversityAndCourseTypeAsync(int universityId, string courseType);
    Task<UniversityCourseTypeModel?> GetWithNavigationsAsync(int id);
}
