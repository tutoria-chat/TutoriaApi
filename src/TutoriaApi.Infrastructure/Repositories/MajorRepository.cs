using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class MajorRepository : Repository<Major>, IMajorRepository
{
    public MajorRepository(TutoriaDbContext context) : base(context) { }

    public async Task<IEnumerable<Major>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Where(m => m.UniversityId == universityId)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsByNameInUniversityAsync(string name, int universityId)
    {
        var normalized = name.Trim().ToLower();
        return await _dbSet.AnyAsync(m =>
            m.UniversityId == universityId && m.Name.ToLower() == normalized);
    }

    public async Task<List<int>> GetValidMajorIdsAsync(IEnumerable<int> majorIds, int universityId)
    {
        var ids = majorIds.Distinct().ToList();
        if (ids.Count == 0) return new List<int>();
        return await _dbSet
            .Where(m => m.UniversityId == universityId && ids.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Major> majors)
    {
        var list = majors.ToList();
        var now = DateTime.UtcNow;
        foreach (var m in list)
        {
            m.CreatedAt = now;
            m.UpdatedAt = now;
        }
        await _dbSet.AddRangeAsync(list);
        await _context.SaveChangesAsync();
    }

    public async Task<HashSet<string>> ExistingNamesLowerAsync(int universityId)
    {
        var names = await _dbSet
            .Where(m => m.UniversityId == universityId)
            .Select(m => m.Name.ToLower())
            .ToListAsync();
        return names.ToHashSet();
    }

    public async Task<IEnumerable<Major>> GetMajorsForCourseAsync(int courseId)
    {
        return await _context.CourseMajors
            .Where(cm => cm.CourseId == courseId)
            .Join(_dbSet, cm => cm.MajorId, m => m.Id, (cm, m) => m)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<Dictionary<int, List<Major>>> GetMajorsForCoursesAsync(IEnumerable<int> courseIds)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<Major>>();

        var rows = await _context.CourseMajors
            .Where(cm => ids.Contains(cm.CourseId))
            .Join(_dbSet, cm => cm.MajorId, m => m.Id, (cm, m) => new { cm.CourseId, Major = m })
            .ToListAsync();

        return rows
            .GroupBy(r => r.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Major).OrderBy(m => m.Name).ToList());
    }

    public async Task SetCourseMajorsAsync(int courseId, IEnumerable<int> majorIds)
    {
        var target = majorIds.Distinct().ToHashSet();

        var existing = await _context.CourseMajors
            .Where(cm => cm.CourseId == courseId)
            .ToListAsync();
        var existingIds = existing.Select(cm => cm.MajorId).ToHashSet();

        var toRemove = existing.Where(cm => !target.Contains(cm.MajorId)).ToList();
        if (toRemove.Count > 0) _context.CourseMajors.RemoveRange(toRemove);

        var toAdd = target
            .Where(id => !existingIds.Contains(id))
            .Select(id => new CourseMajor { CourseId = courseId, MajorId = id, CreatedAt = DateTime.UtcNow })
            .ToList();
        if (toAdd.Count > 0) await _context.CourseMajors.AddRangeAsync(toAdd);

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await _context.SaveChangesAsync();
    }
}
