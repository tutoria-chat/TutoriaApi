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
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(User user);
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByEmailExcludingUserAsync(string email, int excludeUserId);
    Task SaveChangesAsync();
}
