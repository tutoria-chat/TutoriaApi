using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UniversityService : IUniversityService
{
    private readonly IUniversityRepository _universityRepository;

    public UniversityService(IUniversityRepository universityRepository)
    {
        _universityRepository = universityRepository;
    }

    public async Task<University?> GetByIdAsync(int id)
    {
        return await _universityRepository.GetByIdAsync(id);
    }

    public async Task<University?> GetWithCoursesAsync(int id)
    {
        return await _universityRepository.GetByIdWithCoursesAsync(id);
    }

    public async Task<(IEnumerable<University> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize)
    {
        return await _universityRepository.SearchAsync(search, page, pageSize);
    }

    public async Task<University> CreateAsync(University university)
    {
        // Validate: Check if university with same name or code already exists
        var nameExists = await _universityRepository.ExistsByNameAsync(university.Name);
        var codeExists = await _universityRepository.ExistsByCodeAsync(university.Code);

        if (nameExists || codeExists)
        {
            throw new InvalidOperationException("University with this name or code already exists");
        }

        return await _universityRepository.AddAsync(university);
    }

    public async Task<University> UpdateAsync(int id, University university)
    {
        var existing = await _universityRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("University not found");
        }

        existing.Name = university.Name;
        existing.Code = university.Code;
        existing.Description = university.Description;

        await _universityRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var university = await _universityRepository.GetByIdAsync(id);
        if (university == null)
        {
            throw new KeyNotFoundException("University not found");
        }

        await _universityRepository.DeleteAsync(university);
    }
}
