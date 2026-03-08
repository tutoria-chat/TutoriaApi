using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class CourseTypeModelService : ICourseTypeModelService
{
    private readonly ICourseTypeModelRepository _courseTypeModelRepository;
    private readonly IUniversityCourseTypeModelRepository _universityCourseTypeModelRepository;
    private readonly IAIModelRepository _aiModelRepository;

    public CourseTypeModelService(
        ICourseTypeModelRepository courseTypeModelRepository,
        IUniversityCourseTypeModelRepository universityCourseTypeModelRepository,
        IAIModelRepository aiModelRepository)
    {
        _courseTypeModelRepository = courseTypeModelRepository;
        _universityCourseTypeModelRepository = universityCourseTypeModelRepository;
        _aiModelRepository = aiModelRepository;
    }

    public async Task<IEnumerable<CourseTypeModel>> GetActiveAsync()
    {
        return await _courseTypeModelRepository.GetActiveAsync();
    }

    public async Task<IEnumerable<CourseTypeModel>> GetByCourseTypeAsync(string courseType)
    {
        return await _courseTypeModelRepository.GetByCourseTypeAsync(courseType);
    }

    public async Task<CourseTypeModel?> GetByIdAsync(int id)
    {
        return await _courseTypeModelRepository.GetWithAIModelAsync(id);
    }

    public async Task<CourseTypeModel> CreateAsync(CourseTypeModel courseTypeModel)
    {
        // Validate AI model exists
        var aiModel = await _aiModelRepository.GetByIdAsync(courseTypeModel.AIModelId);
        if (aiModel == null)
        {
            throw new InvalidOperationException("AI model not found");
        }

        return await _courseTypeModelRepository.AddAsync(courseTypeModel);
    }

    public async Task<CourseTypeModel> UpdateAsync(int id, CourseTypeModel courseTypeModel)
    {
        var existing = await _courseTypeModelRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Course type model mapping not found");
        }

        // Validate AI model exists
        var aiModel = await _aiModelRepository.GetByIdAsync(courseTypeModel.AIModelId);
        if (aiModel == null)
        {
            throw new InvalidOperationException("AI model not found");
        }

        existing.CourseType = courseTypeModel.CourseType;
        existing.AIModelId = courseTypeModel.AIModelId;
        existing.Priority = courseTypeModel.Priority;
        existing.IsActive = courseTypeModel.IsActive;

        await _courseTypeModelRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _courseTypeModelRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Course type model mapping not found");
        }

        await _courseTypeModelRepository.DeleteAsync(existing);
    }

    public async Task<IEnumerable<UniversityCourseTypeModel>> GetUniversityOverridesAsync(int universityId)
    {
        return await _universityCourseTypeModelRepository.GetByUniversityIdAsync(universityId);
    }

    public async Task<UniversityCourseTypeModel> CreateUniversityOverrideAsync(UniversityCourseTypeModel override_)
    {
        // Validate AI model exists
        var aiModel = await _aiModelRepository.GetByIdAsync(override_.AIModelId);
        if (aiModel == null)
        {
            throw new InvalidOperationException("AI model not found");
        }

        return await _universityCourseTypeModelRepository.AddAsync(override_);
    }
}
