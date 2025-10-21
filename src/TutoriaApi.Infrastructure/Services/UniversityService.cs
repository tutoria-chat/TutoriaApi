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
        existing.Address = university.Address;
        existing.TaxId = university.TaxId;
        existing.ContactEmail = university.ContactEmail;
        existing.ContactPhone = university.ContactPhone;
        existing.ContactPerson = university.ContactPerson;
        existing.Website = university.Website;
        existing.SubscriptionTier = university.SubscriptionTier;

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

    public async Task<int> GetProfessorsCountAsync(int universityId)
    {
        return await _universityRepository.GetProfessorsCountAsync(universityId);
    }

    public async Task<int> GetModulesCountByCourseAsync(int courseId)
    {
        return await _universityRepository.GetModulesCountByCourseAsync(courseId);
    }

    public async Task<int> GetAssignedProfessorsCountByCourseAsync(int courseId)
    {
        return await _universityRepository.GetAssignedProfessorsCountByCourseAsync(courseId);
    }

    public async Task<int> GetStudentsCountByCourseAsync(int courseId)
    {
        return await _universityRepository.GetStudentsCountByCourseAsync(courseId);
    }

    public async Task<UniversityWithCoursesViewModel?> GetUniversityWithDetailsAsync(int id)
    {
        // Get university with courses - EXPLICITLY filtered by navigation property
        var university = await _universityRepository.GetByIdWithCoursesAsync(id);

        if (university == null)
        {
            return null;
        }

        // CRITICAL: Validate that all courses actually belong to this university
        // This is a safety check in case of data corruption
        var validCourses = university.Courses
            .Where(c => c.UniversityId == id)
            .ToList();

        // Get professor count for this university
        var professorsCount = await _universityRepository.GetProfessorsCountAsync(id);

        // Build course view models with counts
        var courseViewModels = new List<CourseViewModel>();
        foreach (var course in validCourses)
        {
            var modulesCount = await _universityRepository.GetModulesCountByCourseAsync(course.Id);
            var courseProfessorsCount = await _universityRepository.GetAssignedProfessorsCountByCourseAsync(course.Id);
            var studentsCount = await _universityRepository.GetStudentsCountByCourseAsync(course.Id);

            courseViewModels.Add(new CourseViewModel
            {
                Course = course,
                ModulesCount = modulesCount,
                ProfessorsCount = courseProfessorsCount,
                StudentsCount = studentsCount
            });
        }

        return new UniversityWithCoursesViewModel
        {
            University = university,
            ProfessorsCount = professorsCount,
            Courses = courseViewModels
        };
    }
}
