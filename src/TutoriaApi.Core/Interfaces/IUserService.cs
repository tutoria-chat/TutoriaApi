using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public class UserListViewModel
{
    public User User { get; set; } = null!;
    public string? UniversityName { get; set; }
}

public interface IUserService
{
    Task<(List<UserListViewModel> Items, int Total)> GetPagedAsync(
        string? userType,
        int? universityId,
        bool? isAdmin,
        bool? isActive,
        string? search,
        int page,
        int pageSize);
    Task<UserListViewModel?> GetByIdAsync(int id);
    Task<UserListViewModel> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        string userType,
        int? universityId,
        int? courseId,
        bool isAdmin,
        string? themePreference,
        string? languagePreference,
        User currentUser);
    Task<UserListViewModel> UpdateAsync(
        int id,
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        bool? isAdmin,
        bool? isActive,
        int? universityId,
        int? courseId,
        string? themePreference,
        string? languagePreference,
        User currentUser);
    Task<UserListViewModel> ActivateAsync(int id, User currentUser);
    Task<UserListViewModel> DeactivateAsync(int id, User currentUser);
    Task DeleteAsync(int id, User currentUser);
    Task ChangePasswordAsync(int id, string newPassword, User currentUser);
}
