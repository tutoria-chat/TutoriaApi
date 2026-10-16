using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CourseLifecycleRepository : ICourseLifecycleRepository
{
    private readonly TutoriaDbContext _context;

    public CourseLifecycleRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<Course>> GetActiveInstitutionCoursesAsync()
    {
        return await _context.Courses
            .Include(c => c.University)
            .Where(c => c.DeletedAt == null && c.DeactivatedAt == null && !c.University.IsConsumer)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<Course?> GetCourseAsync(int courseId)
    {
        return await _context.Courses
            .Include(c => c.University)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.DeletedAt == null);
    }

    public async Task SaveCourseAsync(Course course)
    {
        // Courses come from this context already tracked; Update() would also mark
        // the loaded University as modified.
        if (_context.Entry(course).State == EntityState.Detached)
            _context.Courses.Update(course);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountEnrolledStudentsAsync(int courseId)
    {
        return await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .Join(_context.Users.Where(u => u.UserType == UserTypes.Student),
                sc => sc.StudentId, u => u.UserId, (sc, u) => sc.StudentId)
            .CountAsync();
    }

    public async Task<DateTime?> GetEarliestEnrollmentAsync(int courseId)
    {
        return await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .Join(_context.Users.Where(u => u.UserType == UserTypes.Student),
                sc => sc.StudentId, u => u.UserId, (sc, u) => (DateTime?)sc.CreatedAt)
            .MinAsync();
    }

    public async Task<DateTime?> GetOldestActiveKeyCreatedAtAsync(int courseId, DateTime nowUtc)
    {
        return await _context.ModuleAccessTokens
            .Where(t => t.Module.CourseId == courseId
                        && t.IsActive
                        && (t.ExpiresAt == null || t.ExpiresAt > nowUtc))
            .MinAsync(t => t.CreatedAt);
    }

    public async Task<List<User>> GetAssignedProfessorsAsync(int courseId)
    {
        return await _context.ProfessorCourses
            .Where(pc => pc.CourseId == courseId)
            .Join(_context.Users, pc => pc.ProfessorId, u => u.UserId, (pc, u) => u)
            .Where(u => u.UserType == UserTypes.Professor && u.IsActive && u.Email != "")
            .ToListAsync();
    }

    public async Task<bool> IsProfessorAssignedAsync(int courseId, int professorId)
    {
        return await _context.ProfessorCourses
            .AnyAsync(pc => pc.CourseId == courseId && pc.ProfessorId == professorId);
    }

    public async Task<CourseLifecycleNotification?> GetNotificationAsync(int courseId, string key)
    {
        return await _context.CourseLifecycleNotifications
            .FirstOrDefaultAsync(n => n.CourseId == courseId && n.Key == key);
    }

    public async Task AddNotificationAsync(CourseLifecycleNotification notification)
    {
        await _context.CourseLifecycleNotifications.AddAsync(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<int> RemoveAllStudentsAsync(int courseId)
    {
        var enrollments = await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .ToListAsync();
        _context.StudentCourses.RemoveRange(enrollments);
        await _context.SaveChangesAsync();
        return enrollments.Count;
    }
}
