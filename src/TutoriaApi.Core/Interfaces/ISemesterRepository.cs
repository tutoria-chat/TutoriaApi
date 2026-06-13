using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ISemesterRepository : IRepository<Semester>
{
    Task<List<Semester>> GetByUniversityIdAsync(int universityId);
    Task<Semester?> GetByIdWithUniversityAsync(int id);
}
