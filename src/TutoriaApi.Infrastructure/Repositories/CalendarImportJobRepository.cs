using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class CalendarImportJobRepository : Repository<CalendarImportJob>, ICalendarImportJobRepository
{
    public CalendarImportJobRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CalendarImportJob>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Where(j => j.CourseId == courseId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<CalendarImportJob?> GetWithCourseAsync(int id)
    {
        return await _dbSet
            .Include(j => j.Course)
            .FirstOrDefaultAsync(j => j.Id == id);
    }
}
