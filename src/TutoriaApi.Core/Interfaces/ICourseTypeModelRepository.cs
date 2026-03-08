using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseTypeModelRepository : IRepository<CourseTypeModel>
{
    Task<IEnumerable<CourseTypeModel>> GetByCourseTypeAsync(string courseType);
    Task<IEnumerable<CourseTypeModel>> GetActiveAsync();
    Task<CourseTypeModel?> GetWithAIModelAsync(int id);
}
