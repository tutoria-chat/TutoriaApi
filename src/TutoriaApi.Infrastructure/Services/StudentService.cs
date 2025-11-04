using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class StudentService : IStudentService
{
    private readonly IUserRepository _userRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentCourseRepository _studentCourseRepository;

    public StudentService(
        IUserRepository userRepository,
        ICourseRepository courseRepository,
        IStudentCourseRepository studentCourseRepository)
    {
        _userRepository = userRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
    }

    public async Task<(List<User> Items, int Total)> GetPagedAsync(
        int? courseId,
        string? search,
        int page,
        int pageSize)
    {
        // Get student IDs for course filter if provided
        List<int>? studentIdsInCourse = null;
        if (courseId.HasValue)
        {
            studentIdsInCourse = await _studentCourseRepository.GetStudentIdsByCourseIdAsync(courseId.Value);
        }

        // Use repository to get paged students
        return await _userRepository.GetStudentsPagedAsync(studentIdsInCourse, search, page, pageSize);
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
        int courseId)
    {
        // Check if course exists
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        // Check if username or email already exists
        if (await _userRepository.ExistsByUsernameAsync(username))
        {
            throw new InvalidOperationException("Username already exists");
        }

        if (await _userRepository.ExistsByEmailAsync(email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        var student = new User
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = null, // Students don't have passwords - they don't login
            UserType = "student",
            IsActive = true
        };

        await _userRepository.AddAsync(student);

        // Enroll student in course via junction table
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
        {
            throw new KeyNotFoundException("Student not found");
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            if (await _userRepository.ExistsByUsernameExcludingUserAsync(username, id))
            {
                throw new InvalidOperationException("Username already exists");
            }
            student.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (await _userRepository.ExistsByEmailExcludingUserAsync(email, id))
            {
                throw new InvalidOperationException("Email already exists");
            }
            student.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            student.FirstName = firstName;
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            student.LastName = lastName;
        }

        if (isActive.HasValue)
        {
            student.IsActive = isActive.Value;
        }

        if (courseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(courseId.Value);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }

            // Enroll student in the new course
            await _studentCourseRepository.EnrollStudentInCourseAsync(id, courseId.Value);
        }

        await _userRepository.UpdateAsync(student);

        return student;
    }

    public async Task DeleteAsync(int id)
    {
        var student = await _userRepository.GetStudentByIdAsync(id);
        if (student == null)
        {
            throw new KeyNotFoundException("Student not found");
        }

        await _userRepository.DeleteAsync(student);
    }
}
