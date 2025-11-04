using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ProfessorCourseRepository : IProfessorCourseRepository
{
    private readonly TutoriaDbContext _context;

    public ProfessorCourseRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<int>> GetCourseIdsByProfessorIdAsync(int professorId)
    {
        return await _context.ProfessorCourses
            .Where(pc => pc.ProfessorId == professorId)
            .Select(pc => pc.CourseId)
            .ToListAsync();
    }

    public async Task<List<int>> GetProfessorIdsByCourseIdAsync(int courseId)
    {
        return await _context.ProfessorCourses
            .Where(pc => pc.CourseId == courseId)
            .Select(pc => pc.ProfessorId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetCourseCountsByProfessorIdsAsync(IEnumerable<int> professorIds)
    {
        return await _context.ProfessorCourses
            .Where(pc => professorIds.Contains(pc.ProfessorId))
            .GroupBy(pc => pc.ProfessorId)
            .Select(g => new { ProfessorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProfessorId, x => x.Count);
    }

    public async Task<bool> IsProfessorAssignedToCourseAsync(int professorId, int courseId)
    {
        return await _context.ProfessorCourses
            .AnyAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);
    }

    public async Task AddProfessorToCourseAsync(int professorId, int courseId)
    {
        var exists = await IsProfessorAssignedToCourseAsync(professorId, courseId);
        if (exists)
        {
            return; // Already assigned, no-op
        }

        var professorCourse = new ProfessorCourse
        {
            ProfessorId = professorId,
            CourseId = courseId
        };

        await _context.ProfessorCourses.AddAsync(professorCourse);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveProfessorFromCourseAsync(int professorId, int courseId)
    {
        var professorCourse = await _context.ProfessorCourses
            .FirstOrDefaultAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);

        if (professorCourse != null)
        {
            _context.ProfessorCourses.Remove(professorCourse);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllCoursesForProfessorAsync(int professorId)
    {
        var professorCourses = await _context.ProfessorCourses
            .Where(pc => pc.ProfessorId == professorId)
            .ToListAsync();

        _context.ProfessorCourses.RemoveRange(professorCourses);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAllProfessorsForCourseAsync(int courseId)
    {
        var professorCourses = await _context.ProfessorCourses
            .Where(pc => pc.CourseId == courseId)
            .ToListAsync();

        _context.ProfessorCourses.RemoveRange(professorCourses);
        await _context.SaveChangesAsync();
    }
}
