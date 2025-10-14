using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;

    public ModuleService(IModuleRepository moduleRepository)
    {
        _moduleRepository = moduleRepository;
    }

    public async Task<Module?> GetByIdAsync(int id)
    {
        return await _moduleRepository.GetByIdAsync(id);
    }

    public async Task<Module?> GetWithDetailsAsync(int id)
    {
        return await _moduleRepository.GetWithDetailsAsync(id);
    }

    public async Task<(IEnumerable<Module> Items, int Total)> GetPagedAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize)
    {
        return await _moduleRepository.SearchAsync(courseId, semester, year, search, page, pageSize);
    }

    public async Task<Module> CreateAsync(Module module)
    {
        // Validate: Check if module with same code exists in course
        var exists = await _moduleRepository.ExistsByCodeAndCourseAsync(module.Code, module.CourseId);
        if (exists)
        {
            throw new InvalidOperationException("Module with this code already exists in this course");
        }

        // Validate semester and year constraints
        if (module.Semester < 1 || module.Semester > 8)
        {
            throw new ArgumentException("Semester must be between 1 and 8");
        }

        if (module.Year < 2020 || module.Year > 2050)
        {
            throw new ArgumentException("Year must be between 2020 and 2050");
        }

        return await _moduleRepository.AddAsync(module);
    }

    public async Task<Module> UpdateAsync(int id, Module module)
    {
        var existing = await _moduleRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        existing.Name = module.Name;
        existing.Code = module.Code;
        existing.Description = module.Description;
        existing.SystemPrompt = module.SystemPrompt;
        existing.Semester = module.Semester;
        existing.Year = module.Year;
        existing.CourseId = module.CourseId;
        existing.TutorLanguage = module.TutorLanguage;

        await _moduleRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var module = await _moduleRepository.GetByIdAsync(id);
        if (module == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        await _moduleRepository.DeleteAsync(module);
    }
}
