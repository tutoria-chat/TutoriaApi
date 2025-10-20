using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

// View model for professor list
public class ProfessorListViewModel
{
    public User Professor { get; set; } = null!;
    public string? UniversityName { get; set; }
    public int CoursesCount { get; set; }
}

// View model for professor details
public class ProfessorDetailViewModel
{
    public User Professor { get; set; } = null!;
    public string? UniversityName { get; set; }
    public List<int> AssignedCourseIds { get; set; } = new();
}

public interface IProfessorService
{
    Task<(List<ProfessorListViewModel> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? courseId,
        bool? isAdmin,
        string? search,
        int page,
        int pageSize,
        User? currentUser);
    Task<ProfessorDetailViewModel?> GetByIdAsync(int id, User? currentUser);
    Task<ProfessorDetailViewModel?> GetCurrentProfessorAsync(User currentUser);
    Task<List<int>> GetProfessorCourseIdsAsync(int professorId, User? currentUser);
    Task<ProfessorDetailViewModel> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        int universityId,
        bool isAdmin,
        User currentUser);
    Task<ProfessorDetailViewModel> UpdateAsync(
        int id,
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        bool? isAdmin,
        bool? isActive,
        User currentUser);
    Task DeleteAsync(int id, User currentUser);
    Task ChangePasswordAsync(int id, string newPassword, User currentUser);
}
