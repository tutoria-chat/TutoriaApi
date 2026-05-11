using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly AccessControlHelper _accessControl;
    private readonly IAuditLogService _auditLogService;

    public ModuleService(
        IModuleRepository moduleRepository,
        IFileRepository fileRepository,
        ICourseRepository courseRepository,
        ISqsMessagingService sqsMessagingService,
        AccessControlHelper accessControl,
        IAuditLogService auditLogService)
    {
        _moduleRepository = moduleRepository;
        _fileRepository = fileRepository;
        _courseRepository = courseRepository;
        _sqsMessagingService = sqsMessagingService;
        _accessControl = accessControl;
        _auditLogService = auditLogService;
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
        // Compute scope at the SQL level so pagination is correct and there's no
        // chance of leaking cross-tenant rows even when other filters are absent.
        int? scopeUniversityId = null;
        List<int>? allowedCourseIds = null;

        if (currentUser != null && currentUser.UserType != UserTypes.SuperAdmin)
        {
            // Every non-super-admin caller is restricted to their own university.
            scopeUniversityId = currentUser.UniversityId;

            // Regular professors are further restricted to their assigned courses.
            // An empty list means "no assignments yet" and must yield zero results.
            if (currentUser.UserType == UserTypes.Professor && !(currentUser.IsAdmin ?? false))
            {
                allowedCourseIds = (await _accessControl.GetProfessorCourseIdsAsync(currentUser.UserId)).ToList();
            }
        }

        var (modules, total) = await _moduleRepository.SearchAsync(
            courseId, semester, year, search, page, pageSize, scopeUniversityId, allowedCourseIds);

        // Build view models with counts
        var moduleIds = modules.Select(m => m.Id).ToList();
        var tokenCounts = await _moduleRepository.GetTokenCountsAsync(moduleIds);

        var viewModels = new List<ModuleListViewModel>();
        foreach (var module in modules)
        {
            var filesCount = (await _fileRepository.GetByModuleIdAsync(module.Id)).Count();
            tokenCounts.TryGetValue(module.Id, out var tokensCount);

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

    public async Task<Module> CreateAsync(Module module, User currentUser)
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

        // Auto-enumerate module: Add "Module X - " prefix based on creation order
        var modulesInCourse = await _moduleRepository.GetByCourseIdAsync(module.CourseId);
        var moduleNumber = modulesInCourse.Count() + 1;

        // Get localized "Module" word based on TutorLanguage
        var moduleWord = GetLocalizedModuleWord(module.TutorLanguage);

        // Only add prefix if the name doesn't already start with any localized "Module" word
        var moduleWordPrefixes = new[] { "Module ", "Módulo ", "Modulo " };
        var hasModulePrefix = moduleWordPrefixes.Any(prefix =>
            module.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!hasModulePrefix)
        {
            module.Name = $"{moduleWord} {moduleNumber} - {module.Name}";
        }

        var created = await _moduleRepository.AddAsync(module);

        // Get course to retrieve university ID for audit log
        var course = await _courseRepository.GetByIdAsync(created.CourseId);

        // Audit log: Module created
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: course?.UniversityId,
            action: "Create",
            entityType: "Module",
            entityId: created.Id,
            entityName: created.Name,
            changes: null);

        return created;
    }

    public async Task<Module> UpdateAsync(int id, Module module, User currentUser)
    {
        var existing = await _moduleRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        // Track changes for audit log
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>();

        if (existing.Name != module.Name)
            changes["Name"] = (existing.Name, module.Name);

        if (existing.Code != module.Code)
            changes["Code"] = (existing.Code, module.Code);

        if (existing.Description != module.Description)
            changes["Description"] = (existing.Description, module.Description);

        if (existing.SystemPrompt != module.SystemPrompt)
            changes["SystemPrompt"] = (existing.SystemPrompt, module.SystemPrompt);

        if (existing.Semester != module.Semester)
            changes["Semester"] = (existing.Semester, module.Semester);

        if (existing.Year != module.Year)
            changes["Year"] = (existing.Year, module.Year);

        if (existing.TutorLanguage != module.TutorLanguage)
            changes["TutorLanguage"] = (existing.TutorLanguage, module.TutorLanguage);

        if (existing.AIModelId != module.AIModelId)
            changes["AIModelId"] = (existing.AIModelId, module.AIModelId);

        // Apply updates
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

        // Get course to retrieve university ID for audit log
        var course = await _courseRepository.GetByIdAsync(existing.CourseId);

        // Audit log: Only log if there were actual changes
        if (changes.Any())
        {
            await _auditLogService.LogAsync(
                userId: currentUser.UserId,
                username: currentUser.Username,
                universityId: course?.UniversityId,
                action: "Update",
                entityType: "Module",
                entityId: existing.Id,
                entityName: existing.Name,
                changes: changes);
        }

        return existing;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var module = await _moduleRepository.GetByIdAsync(id);
        if (module == null || !module.IsActive)
        {
            throw new KeyNotFoundException("Module not found");
        }

        // Get course to retrieve university ID for audit log
        var course = await _courseRepository.GetByIdAsync(module.CourseId);

        // Soft delete — keeps data intact for future cleanup job
        module.IsActive = false;
        await _moduleRepository.UpdateAsync(module);

        // Audit log: Module soft-deleted
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: course?.UniversityId,
            action: "Delete",
            entityType: "Module",
            entityId: module.Id,
            entityName: module.Name,
            changes: null);
    }

    public async Task<(int QueuedCount, int TotalFiles)> QueueExtractionForAllFilesAsync(int moduleId, bool force)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module == null || !module.IsActive)
            throw new KeyNotFoundException($"Module {moduleId} not found");

        var files = await _fileRepository.GetByModuleIdAsync(moduleId);
        var activeFiles = files.Where(f => f.IsActive).ToList();

        int queued = 0;
        foreach (var file in activeFiles)
        {
            if (!force && file.ProcessingStatus == "ready")
                continue;

            var sent = await _sqsMessagingService.SendExtractionJobAsync(file.Id, moduleId);
            if (sent) queued++;
        }

        return (queued, activeFiles.Count);
    }

    /// <summary>
    /// Gets the localized word for "Module" based on the tutor language.
    /// </summary>
    private static string GetLocalizedModuleWord(string tutorLanguage)
    {
        return tutorLanguage?.ToLower() switch
        {
            "pt-br" => "Módulo",
            "es" => "Módulo",
            _ => "Module" // English and fallback
        };
    }
}
