using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ICalendarImportJobRepository : IRepository<CalendarImportJob>
{
    Task<IEnumerable<CalendarImportJob>> GetByCourseIdAsync(int courseId);
    Task<CalendarImportJob?> GetWithCourseAsync(int id);
}
