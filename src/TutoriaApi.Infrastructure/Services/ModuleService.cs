using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IFileRepository _fileRepository;
    private readonly AccessControlHelper _accessControl;

    public ModuleService(
        IModuleRepository moduleRepository,
        IFileRepository fileRepository,
        AccessControlHelper accessControl)
    {
        _moduleRepository = moduleRepository;
        _fileRepository = fileRepository;
        _accessControl = accessControl;
    }

    public async Task<Module?> GetByIdAsync(int id)
    {
        return await _moduleRepository.GetByIdAsync(id);
    }

    public async Task<ModuleDetailViewModel?> GetWithDetailsAsync(int id)
    {
        var module = await _moduleRepository.GetWithDetailsAsync(id);
        if (module == null) return null;

        var files = await _fileRepository.GetByModuleIdAsync(id);

        return new ModuleDetailViewModel
        {
            Module = module,
            Course = module.Course,
            AIModel = module.AIModel,
            Files = files.ToList()
        };
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

    public async Task<(List<ModuleListViewModel> Items, int Total)> GetPagedWithCountsAsync(
        int? courseId,
        int? semester,
        int? year,
        string? search,
        int page,
        int pageSize,
        User? currentUser)
    {
        // Get accessible course IDs based on user role
        List<int>? allowedCourseIds = null;

        if (currentUser != null)
        {
            if (currentUser.UserType == "professor")
            {
                if (currentUser.IsAdmin ?? false)
                {
                    // Admin professors can access all courses in their university
                    // We'll filter by university in the query parameters
                }
                else
                {
                    // Regular professors can only access assigned courses
                    allowedCourseIds = (await _accessControl.GetProfessorCourseIdsAsync(currentUser.UserId)).ToList();
                }
            }
            // Super admins can access all (no filtering)
        }

        // Get modules with applied filters and access control
        var (modules, total) = await _moduleRepository.SearchAsync(courseId, semester, year, search, page, pageSize);

        // Apply professor access control filter
        if (allowedCourseIds != null && allowedCourseIds.Any())
        {
            modules = modules.Where(m => allowedCourseIds.Contains(m.CourseId));
            total = modules.Count();  // Recalculate total after filtering
        }

        // Build view models with counts
        var viewModels = new List<ModuleListViewModel>();
        foreach (var module in modules)
        {
            var filesCount = (await _fileRepository.GetByModuleIdAsync(module.Id)).Count();
            // TODO: Add tokens count when IModuleAccessTokenRepository is implemented
            var tokensCount = 0;

            viewModels.Add(new ModuleListViewModel
            {
                Module = module,
                CourseName = module.Course?.Name,
                AIModelDisplayName = module.AIModel?.DisplayName,
                FilesCount = filesCount,
                TokensCount = tokensCount
            });
        }

        return (viewModels, total);
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
        if (module.Semester.HasValue && (module.Semester < 1 || module.Semester > 2))
        {
            throw new ArgumentException("Semester must be 1 or 2 (only two semesters per year)");
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
        existing.AIModelId = module.AIModelId;

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
