using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICourseTypeModelService
{
    Task<IEnumerable<CourseTypeModel>> GetActiveAsync();
    Task<IEnumerable<CourseTypeModel>> GetByCourseTypeAsync(string courseType);
    Task<CourseTypeModel?> GetByIdAsync(int id);
    Task<CourseTypeModel> CreateAsync(CourseTypeModel courseTypeModel);
    Task<CourseTypeModel> UpdateAsync(int id, CourseTypeModel courseTypeModel);
    Task DeleteAsync(int id);

    // University overrides
    Task<IEnumerable<UniversityCourseTypeModel>> GetUniversityOverridesAsync(int universityId);
    Task<UniversityCourseTypeModel> CreateUniversityOverrideAsync(UniversityCourseTypeModel override_);
}
