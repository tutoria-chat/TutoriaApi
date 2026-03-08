using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IStudentImportJobRepository : IRepository<StudentImportJob>
{
    Task<IEnumerable<StudentImportJob>> GetByCourseIdAsync(int courseId);
    Task<IEnumerable<StudentImportJob>> GetByUniversityIdAsync(int universityId);
}
