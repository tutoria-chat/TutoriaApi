using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class MajorService : IMajorService
{
    private readonly IMajorRepository _majorRepository;

    public MajorService(IMajorRepository majorRepository)
    {
        _majorRepository = majorRepository;
    }

    public Task<IEnumerable<Major>> GetByUniversityAsync(int universityId)
        => _majorRepository.GetByUniversityIdAsync(universityId);

    public Task<IEnumerable<Major>> GetForCourseAsync(int courseId)
        => _majorRepository.GetMajorsForCourseAsync(courseId);

    public async Task<Major> CreateAsync(int universityId, string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Major name is required");
        }

        if (await _majorRepository.ExistsByNameInUniversityAsync(trimmed, universityId))
        {
            throw new InvalidOperationException("A major with this name already exists at this university");
        }

        return await _majorRepository.AddAsync(new Major { UniversityId = universityId, Name = trimmed });
    }

    public async Task DeleteAsync(int universityId, int majorId)
    {
        var major = await _majorRepository.GetByIdAsync(majorId);
        if (major == null || major.UniversityId != universityId)
        {
            throw new KeyNotFoundException("Major not found");
        }

        // The CourseMajors join rows cascade away with the major (FK OnDelete Cascade).
        await _majorRepository.DeleteAsync(major);
    }

    public async Task<IEnumerable<Major>> SeedDefaultsAsync(int universityId)
    {
        var existing = await _majorRepository.ExistingNamesLowerAsync(universityId);
        var toAdd = StandardMajors.Names
            .Where(n => !existing.Contains(n.ToLower()))
            .Select(n => new Major { UniversityId = universityId, Name = n })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _majorRepository.AddRangeAsync(toAdd);
        }

        return await _majorRepository.GetByUniversityIdAsync(universityId);
    }

    public async Task SetCourseMajorsAsync(int courseId, int universityId, IEnumerable<int> majorIds)
    {
        // Only tag with majors that actually belong to the course's university.
        var valid = await _majorRepository.GetValidMajorIdsAsync(majorIds, universityId);
        await _majorRepository.SetCourseMajorsAsync(courseId, valid);
    }
}
