using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IUserRepository _userRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IAuditLogService _auditLogService;

    public CourseService(
        ICourseRepository courseRepository,
        IUniversityRepository universityRepository,
        IUserRepository userRepository,
        IModuleRepository moduleRepository,
        IAuditLogService auditLogService)
    {
        _courseRepository = courseRepository;
        _universityRepository = universityRepository;
        _userRepository = userRepository;
        _moduleRepository = moduleRepository;
        _auditLogService = auditLogService;
    }

    public async Task<CourseWithCountsViewModel?> GetCourseWithCountsAsync(int id)
    {
        var course = await _courseRepository.GetWithDetailsAsync(id);

        if (course == null)
        {
            return null;
        }

        var modulesCount = await _courseRepository.GetModulesCountAsync(id);
        var professorsCount = await _courseRepository.GetProfessorsCountAsync(id);
        var studentsCount = await _courseRepository.GetStudentsCountAsync(id);

        return new CourseWithCountsViewModel
        {
            Course = course,
            UniversityName = course.University?.Name,
            ModulesCount = modulesCount,
            ProfessorsCount = professorsCount,
            StudentsCount = studentsCount
        };
    }

    public async Task<CourseDetailViewModel?> GetCourseWithFullDetailsAsync(int id)
    {
        var course = await _courseRepository.GetWithFullDetailsAsync(id);

        if (course == null)
        {
            return null;
        }

        // Get module IDs
        var moduleIds = course.Modules?.Select(m => m.Id).ToList() ?? new List<int>();

        // Get counts from repository
        var fileCounts = await _moduleRepository.GetFileCountsAsync(moduleIds);
        var tokenCounts = await _moduleRepository.GetTokenCountsAsync(moduleIds);

        // Get students for this course via StudentCourses
        var students = new List<User>(); // TODO: Implement student retrieval via repository

        return new CourseDetailViewModel
        {
            Course = course,
            University = course.University,
            Modules = course.Modules?.ToList() ?? new List<Module>(),
            ModuleFileCounts = fileCounts,
            ModuleTokenCounts = tokenCounts,
            Students = students
        };
    }

    public async Task<(List<CourseWithCountsViewModel> Items, int Total)> GetPagedWithCountsAsync(
        int? universityId,
        int? professorId,
        string? search,
        int page,
        int pageSize,
        User? currentUser)
    {
        // Regular professors are restricted to their own assigned courses regardless
        // of any professorId the caller passed. Other roles fall through to whatever
        // filter the controller built (already university-scoped upstream).
        if (currentUser != null
            && currentUser.UserType == UserTypes.Professor
            && !(currentUser.IsAdmin ?? false))
        {
            professorId = currentUser.UserId;
        }

        var (courses, total) = await _courseRepository.SearchAsync(universityId, professorId, search, page, pageSize);

        var viewModels = new List<CourseWithCountsViewModel>();

        foreach (var course in courses)
        {
            // Get university name
            var university = await _universityRepository.GetByIdAsync(course.UniversityId);

            // Get counts
            var modulesCount = await _courseRepository.GetModulesCountAsync(course.Id);
            var professorsCount = await _courseRepository.GetProfessorsCountAsync(course.Id);
            var studentsCount = await _courseRepository.GetStudentsCountAsync(course.Id);

            viewModels.Add(new CourseWithCountsViewModel
            {
                Course = course,
                UniversityName = university?.Name,
                ModulesCount = modulesCount,
                ProfessorsCount = professorsCount,
                StudentsCount = studentsCount
            });
        }

        return (viewModels, total);
    }

    public async Task<Course> CreateAsync(Course course, User currentUser)
    {
        // Validate: Check if university exists
        var university = await _universityRepository.GetByIdAsync(course.UniversityId);
        if (university == null)
        {
            throw new KeyNotFoundException("University not found");
        }

        // Validate: Check if course with same code exists in university
        var exists = await _courseRepository.ExistsByCodeAndUniversityAsync(course.Code, course.UniversityId);
        if (exists)
        {
            throw new InvalidOperationException("Course with this code already exists in this university");
        }

        var created = await _courseRepository.AddAsync(course);

        // Audit log: Course created
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: created.UniversityId,
            action: "Create",
            entityType: "Course",
            entityId: created.Id,
            entityName: created.Name,
            changes: null);

        return created;
    }

    public async Task<CourseWithCountsViewModel> UpdateAsync(int id, Course course, User currentUser)
    {
        var existing = await _courseRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        // Track changes for audit log
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>();

        // Check if code is being changed and if it conflicts with another course
        if (!string.IsNullOrEmpty(course.Code) && course.Code != existing.Code)
        {
            var codeExists = await _courseRepository.ExistsByCodeAndUniversityAsync(course.Code, existing.UniversityId);
            if (codeExists)
            {
                throw new InvalidOperationException("Course with this code already exists in this university");
            }
            changes["Code"] = (existing.Code, course.Code);
            existing.Code = course.Code;
        }

        // Update properties
        if (!string.IsNullOrEmpty(course.Name) && course.Name != existing.Name)
        {
            changes["Name"] = (existing.Name, course.Name);
            existing.Name = course.Name;
        }

        if (course.Description != null && course.Description != existing.Description)
        {
            changes["Description"] = (existing.Description, course.Description);
            existing.Description = course.Description;
        }

        if (course.ExternalCourseId != existing.ExternalCourseId)
        {
            changes["ExternalCourseId"] = (existing.ExternalCourseId, course.ExternalCourseId);
            existing.ExternalCourseId = course.ExternalCourseId;
        }

        // null = not provided (keep); empty = clear; otherwise set the CSV
        if (course.TitleTracks != null)
        {
            var normalized = string.IsNullOrWhiteSpace(course.TitleTracks) ? null : course.TitleTracks.Trim();
            if (normalized != existing.TitleTracks)
            {
                changes["TitleTracks"] = (existing.TitleTracks, normalized);
                existing.TitleTracks = normalized;
            }
        }

        await _courseRepository.UpdateAsync(existing);

        // Audit log: Only log if there were actual changes
        if (changes.Any())
        {
            await _auditLogService.LogAsync(
                userId: currentUser.UserId,
                username: currentUser.Username,
                universityId: existing.UniversityId,
                action: "Update",
                entityType: "Course",
                entityId: existing.Id,
                entityName: existing.Name,
                changes: changes);
        }

        // Return updated course with counts
        return (await GetCourseWithCountsAsync(id))!;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        await _courseRepository.DeleteAsync(course);

        // Audit log: Course deleted
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: course.UniversityId,
            action: "Delete",
            entityType: "Course",
            entityId: course.Id,
            entityName: course.Name,
            changes: null);
    }

    public async Task AssignProfessorAsync(int courseId, int professorId, User currentUser)
    {
        // Check if course exists
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        // Check if professor exists in Users table (unified table)
        var professor = await _userRepository.GetByIdAsync(professorId);
        if (professor == null || professor.UserType != "professor")
        {
            throw new KeyNotFoundException("Professor not found");
        }

        // Check if already assigned
        var isAssigned = await _courseRepository.IsProfessorAssignedAsync(courseId, professorId);
        if (isAssigned)
        {
            throw new InvalidOperationException("Professor is already assigned to this course");
        }

        await _courseRepository.AssignProfessorAsync(courseId, professorId);

        // Audit log: Professor assigned to course
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: course.UniversityId,
            action: "AssignProfessor",
            entityType: "ProfessorCourseAssignment",
            entityId: courseId,
            entityName: $"{professor.Username} → {course.Name}",
            changes: null);
    }

    public async Task UnassignProfessorAsync(int courseId, int professorId)
    {
        var isAssigned = await _courseRepository.IsProfessorAssignedAsync(courseId, professorId);
        if (!isAssigned)
        {
            throw new KeyNotFoundException("Professor is not assigned to this course");
        }

        await _courseRepository.UnassignProfessorAsync(courseId, professorId);
    }
}
