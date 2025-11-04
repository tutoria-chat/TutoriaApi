using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class StudentCourseRepository : IStudentCourseRepository
{
    private readonly TutoriaDbContext _context;

    public StudentCourseRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<int>> GetStudentIdsByCourseIdAsync(int courseId)
    {
        return await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .Select(sc => sc.StudentId)
            .ToListAsync();
    }

    public async Task<List<int>> GetCourseIdsByStudentIdAsync(int studentId)
    {
        return await _context.StudentCourses
            .Where(sc => sc.StudentId == studentId)
            .Select(sc => sc.CourseId)
            .ToListAsync();
    }

    public async Task<bool> IsStudentEnrolledInCourseAsync(int studentId, int courseId)
    {
        return await _context.StudentCourses
            .AnyAsync(sc => sc.StudentId == studentId && sc.CourseId == courseId);
    }

    public async Task EnrollStudentInCourseAsync(int studentId, int courseId)
    {
        var exists = await IsStudentEnrolledInCourseAsync(studentId, courseId);
        if (exists)
        {
            return; // Already enrolled, no-op
        }

        var studentCourse = new StudentCourse
        {
            StudentId = studentId,
            CourseId = courseId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.StudentCourses.AddAsync(studentCourse);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveStudentFromCourseAsync(int studentId, int courseId)
    {
        var studentCourse = await _context.StudentCourses
            .FirstOrDefaultAsync(sc => sc.StudentId == studentId && sc.CourseId == courseId);

        if (studentCourse != null)
        {
            _context.StudentCourses.Remove(studentCourse);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllCoursesForStudentAsync(int studentId)
    {
        var studentCourses = await _context.StudentCourses
            .Where(sc => sc.StudentId == studentId)
            .ToListAsync();

        _context.StudentCourses.RemoveRange(studentCourses);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAllStudentsForCourseAsync(int courseId)
    {
        var studentCourses = await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .ToListAsync();

        _context.StudentCourses.RemoveRange(studentCourses);
        await _context.SaveChangesAsync();
    }
}
