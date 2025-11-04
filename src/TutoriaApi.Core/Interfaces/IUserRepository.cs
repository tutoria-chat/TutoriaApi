using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByIdWithIncludesAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByUsernameWithIncludesAsync(string username);
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPasswordResetTokenAsync(string token);
    Task<IEnumerable<User>> GetByTypeAsync(string userType);
    Task<IEnumerable<User>> GetByUniversityIdAsync(int universityId);
    Task<(List<User> Items, int Total)> GetPagedAsync(
        string? userType,
        int? universityId,
        bool? isAdmin,
        bool? isActive,
        string? search,
        int page,
        int pageSize);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(User user);
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByUsernameExcludingUserAsync(string username, int excludeUserId);
    Task<bool> ExistsByEmailExcludingUserAsync(string email, int excludeUserId);
    Task SaveChangesAsync();

    // Additional methods for Professor/Student services
    Task<(List<User> Items, int Total)> GetProfessorsPagedAsync(
        int? universityId,
        List<int>? filterByProfessorIds,
        bool? isAdmin,
        string? search,
        int page,
        int pageSize);

    Task<(List<User> Items, int Total)> GetStudentsPagedAsync(
        List<int>? filterByStudentIds,
        string? search,
        int page,
        int pageSize);

    Task<User?> GetProfessorByIdWithUniversityAsync(int professorId);
    Task<User?> GetStudentByIdAsync(int studentId);
}
