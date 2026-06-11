using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class StudentService : IStudentService
{
    private readonly IUserRepository _userRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentCourseRepository _studentCourseRepository;
    private readonly TutoriaDbContext _dbContext;

    public StudentService(
        IUserRepository userRepository,
        ICourseRepository courseRepository,
        IStudentCourseRepository studentCourseRepository,
        TutoriaDbContext dbContext)
    {
        _userRepository = userRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _dbContext = dbContext;
    }

    public async Task<(List<User> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? courseId,
        string? search,
        int page,
        int pageSize)
    {
        List<int>? studentIdsFilter = null;

        if (universityId.HasValue)
        {
            var courseIds = await _dbContext.Courses
                .Where(c => c.UniversityId == universityId.Value)
                .Select(c => c.Id)
                .ToListAsync();

            studentIdsFilter = await _dbContext.StudentCourses
                .Where(sc => courseIds.Contains(sc.CourseId))
                .Select(sc => sc.StudentId)
                .Distinct()
                .ToListAsync();
        }

        if (courseId.HasValue)
        {
            var studentIdsInCourse = await _studentCourseRepository.GetStudentIdsByCourseIdAsync(courseId.Value);

            if (studentIdsFilter != null)
            {
                studentIdsFilter = studentIdsFilter.Intersect(studentIdsInCourse).ToList();
            }
            else
            {
                studentIdsFilter = studentIdsInCourse;
            }
        }

        return await _userRepository.GetStudentsPagedAsync(studentIdsFilter, search, page, pageSize);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _userRepository.GetStudentByIdAsync(id);
    }

    public async Task<User> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string externalId,
        int courseId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
            throw new KeyNotFoundException("Course not found");

        if (string.IsNullOrWhiteSpace(externalId))
            throw new InvalidOperationException("Matricula (external ID) is required for students");

        if (await _userRepository.ExistsByUsernameAsync(username))
            throw new InvalidOperationException("Username already exists");

        if (await _userRepository.ExistsByEmailAsync(email))
            throw new InvalidOperationException("Email already exists");

        if (await _userRepository.StudentExistsByExternalIdAsync(externalId.Trim(), course.UniversityId))
            throw new InvalidOperationException("A student with this matricula already exists in this university");

        var student = new User
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = null,
            UserType = "student",
            ExternalId = externalId.Trim(),
            UniversityId = course.UniversityId,
            IsActive = true
        };

        await _userRepository.AddAsync(student);

        // Per-university matricula (multi-tenant direct login)
        var membership = await _dbContext.UserUniversities
            .FirstOrDefaultAsync(uu => uu.UserId == student.UserId && uu.UniversityId == course.UniversityId);
        if (membership == null)
        {
            _dbContext.UserUniversities.Add(new UserUniversity
            {
                UserId = student.UserId,
                UniversityId = course.UniversityId,
                JoinedAt = DateTime.UtcNow,
                ExternalId = externalId.Trim()
            });
            await _dbContext.SaveChangesAsync();
        }

        await _studentCourseRepository.EnrollStudentInCourseAsync(student.UserId, courseId);

        return student;
    }

    public async Task<User> UpdateAsync(
        int id,
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        bool? isActive,
        int? courseId)
    {
        var student = await _userRepository.GetStudentByIdAsync(id);
        if (student == null)
            throw new KeyNotFoundException("Student not found");

        if (!string.IsNullOrWhiteSpace(username))
        {
            if (await _userRepository.ExistsByUsernameExcludingUserAsync(username, id))
                throw new InvalidOperationException("Username already exists");
            student.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (await _userRepository.ExistsByEmailExcludingUserAsync(email, id))
                throw new InvalidOperationException("Email already exists");
            student.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(firstName))
            student.FirstName = firstName;

        if (!string.IsNullOrWhiteSpace(lastName))
            student.LastName = lastName;

        if (isActive.HasValue)
            student.IsActive = isActive.Value;

        if (courseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(courseId.Value);
            if (course == null)
                throw new KeyNotFoundException("Course not found");

            await _studentCourseRepository.EnrollStudentInCourseAsync(id, courseId.Value);
        }

        await _userRepository.UpdateAsync(student);
        return student;
    }

    public async Task UnenrollFromCourseAsync(int studentId, int courseId)
    {
        var student = await _userRepository.GetStudentByIdAsync(studentId);
        if (student == null)
            throw new KeyNotFoundException("Student not found");

        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
            throw new KeyNotFoundException("Course not found");

        var isEnrolled = await _studentCourseRepository.IsStudentEnrolledInCourseAsync(studentId, courseId);
        if (!isEnrolled)
            throw new InvalidOperationException("Student is not enrolled in this course");

        await _studentCourseRepository.RemoveStudentFromCourseAsync(studentId, courseId);
    }

    public async Task DeleteAsync(int id)
    {
        var student = await _userRepository.GetStudentByIdAsync(id);
        if (student == null)
            throw new KeyNotFoundException("Student not found");

        await _userRepository.DeleteAsync(student);
    }

    public async Task<int> GetStudentCountByUniversityAsync(int universityId)
    {
        var courseIds = await _dbContext.Courses
            .Where(c => c.UniversityId == universityId)
            .Select(c => c.Id)
            .ToListAsync();

        return await _dbContext.StudentCourses
            .Where(sc => courseIds.Contains(sc.CourseId))
            .Select(sc => sc.StudentId)
            .Distinct()
            .CountAsync();
    }
}
