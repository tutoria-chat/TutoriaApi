using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IStudentService
{
    Task<(List<User> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? courseId,
        string? search,
        int page,
        int pageSize);
    Task<User?> GetByIdAsync(int id);
    Task<User> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string externalId,
        int courseId);
    Task<User> UpdateAsync(
        int id,
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        bool? isActive,
        int? courseId);
    Task DeleteAsync(int id);
    Task UnenrollFromCourseAsync(int studentId, int courseId);
    Task<int> GetStudentCountByUniversityAsync(int universityId);
}
