using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IStudentService
{
    Task<(List<User> Items, int Total)> GetPagedAsync(
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
}
