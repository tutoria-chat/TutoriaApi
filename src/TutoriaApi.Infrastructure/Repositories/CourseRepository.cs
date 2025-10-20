using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CourseRepository : Repository<Course>, ICourseRepository
{
    public CourseRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<Course?> GetWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.University)
            .Include(c => c.Modules)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Course>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Where(c => c.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Course> Items, int Total)> SearchAsync(
        int? universityId,
        string? search,
        int page,
        int pageSize)
    {
        var query = _dbSet.AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(c => c.UniversityId == universityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsByCodeAndUniversityAsync(string code, int universityId)
    {
        return await _dbSet.AnyAsync(c => c.Code == code && c.UniversityId == universityId);
    }

    public async Task<Course?> GetWithFullDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.University)
            .Include(c => c.Modules)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> GetModulesCountAsync(int courseId)
    {
        return await _context.Modules
            .Where(m => m.CourseId == courseId)
            .CountAsync();
    }

    public async Task<int> GetProfessorsCountAsync(int courseId)
    {
        return await _context.ProfessorCourses
            .Where(pc => pc.CourseId == courseId)
            .CountAsync();
    }

    public async Task<int> GetStudentsCountAsync(int courseId)
    {
        return await _context.StudentCourses
            .Where(sc => sc.CourseId == courseId)
            .CountAsync();
    }

    public async Task<bool> IsProfessorAssignedAsync(int courseId, int professorId)
    {
        return await _context.ProfessorCourses
            .AnyAsync(pc => pc.CourseId == courseId && pc.ProfessorId == professorId);
    }

    public async Task AssignProfessorAsync(int courseId, int professorId)
    {
        var professorCourse = new ProfessorCourse
        {
            CourseId = courseId,
            ProfessorId = professorId
        };

        await _context.ProfessorCourses.AddAsync(professorCourse);
        await _context.SaveChangesAsync();
    }

    public async Task UnassignProfessorAsync(int courseId, int professorId)
    {
        var professorCourse = await _context.ProfessorCourses
            .FirstOrDefaultAsync(pc => pc.CourseId == courseId && pc.ProfessorId == professorId);

        if (professorCourse != null)
        {
            _context.ProfessorCourses.Remove(professorCourse);
            await _context.SaveChangesAsync();
        }
    }
}
