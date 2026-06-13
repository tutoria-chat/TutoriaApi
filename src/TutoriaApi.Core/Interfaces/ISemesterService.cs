using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ISemesterService
{
    Task<List<Semester>> GetByUniversityAsync(int universityId);
    Task<Semester?> GetByIdAsync(int id);
    Task<Semester> CreateAsync(Semester semester);
    Task<Semester> UpdateAsync(int id, string? label, DateTime? startsAtUtc, DateTime? endsAtUtc);
    Task DeleteAsync(int id);
}
