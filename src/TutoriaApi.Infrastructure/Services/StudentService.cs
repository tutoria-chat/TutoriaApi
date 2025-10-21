using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class StudentService : IStudentService
{
    private readonly TutoriaDbContext _context;

    public StudentService(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<(List<User> Items, int Total)> GetPagedAsync(
        int? courseId,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Users
            .Where(u => u.UserType == "student")
            .AsQueryable();

        if (courseId.HasValue)
        {
            var studentIdsInCourse = _context.StudentCourses
                .Where(sc => sc.CourseId == courseId.Value)
                .Select(sc => sc.StudentId);
            query = query.Where(u => studentIdsInCourse.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var students = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (students, total);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == id);
    }

    public async Task<User> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        int courseId)
    {
        // Check if course exists
        var course = await _context.Courses.FindAsync(courseId);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        // Check if username or email already exists
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (existingByUsername != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingByEmail != null)
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

        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        // TODO: Handle course assignment via StudentCourses junction table when table exists
        // var studentCourse = new StudentCourse { StudentId = student.UserId, CourseId = courseId };
        // _context.StudentCourses.Add(studentCourse);
        // await _context.SaveChangesAsync();

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
        var student = await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (student == null)
        {
            throw new KeyNotFoundException("Student not found");
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.UserId != id);

            if (existingByUsername != null)
            {
                throw new InvalidOperationException("Username already exists");
            }

            student.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.UserId != id);

            if (existingByEmail != null)
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
            var course = await _context.Courses.FindAsync(courseId.Value);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }

            // TODO: Handle course assignment via StudentCourses junction table
        }

        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return student;
    }

    public async Task DeleteAsync(int id)
    {
        var student = await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (student == null)
        {
            throw new KeyNotFoundException("Student not found");
        }

        _context.Users.Remove(student);
        await _context.SaveChangesAsync();
    }
}
