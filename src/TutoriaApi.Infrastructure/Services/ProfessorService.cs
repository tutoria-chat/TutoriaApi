using BCrypt.Net;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;

namespace TutoriaApi.Infrastructure.Services;

public class ProfessorService : IProfessorService
{
    private readonly IUserRepository _userRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IProfessorCourseRepository _professorCourseRepository;
    private readonly AccessControlHelper _accessControl;

    public ProfessorService(
        IUserRepository userRepository,
        IUniversityRepository universityRepository,
        IProfessorCourseRepository professorCourseRepository,
        AccessControlHelper accessControl)
    {
        _userRepository = userRepository;
        _universityRepository = universityRepository;
        _professorCourseRepository = professorCourseRepository;
        _accessControl = accessControl;
    }

    public async Task<(List<ProfessorListViewModel> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? courseId,
        bool? isAdmin,
        string? search,
        int page,
        int pageSize,
        User? currentUser)
    {
        // Access control: Only admins can see professors
        if (currentUser != null && currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Non-admin professors cannot list professors");
            }

            // Admin professors can only see professors from their own university
            if (!universityId.HasValue && currentUser.UniversityId.HasValue)
            {
                universityId = currentUser.UniversityId.Value;
            }
        }

        // Get professor IDs for course filter if provided
        List<int>? professorIdsForCourse = null;
        if (courseId.HasValue)
        {
            professorIdsForCourse = await _professorCourseRepository.GetProfessorIdsByCourseIdAsync(courseId.Value);
        }

        // Use repository to get paged professors
        var (professors, total) = await _userRepository.GetProfessorsPagedAsync(
            universityId,
            professorIdsForCourse,
            isAdmin,
            search,
            page,
            pageSize);

        // Get professor IDs to count courses
        var professorIds = professors.Select(p => p.UserId).ToList();

        // Count courses for each professor
        var courseCounts = await _professorCourseRepository.GetCourseCountsByProfessorIdsAsync(professorIds);

        var viewModels = professors.Select(u => new ProfessorListViewModel
        {
            Professor = u,
            UniversityName = u.University?.Name,
            CoursesCount = courseCounts.TryGetValue(u.UserId, out var count) ? count : 0
        }).ToList();

        return (viewModels, total);
    }

    public async Task<ProfessorDetailViewModel?> GetByIdAsync(int id, User? currentUser)
    {
        // Access control: Only admins can view other professors
        if (currentUser != null && currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                throw new UnauthorizedAccessException("You can only view your own profile");
            }
        }

        var professor = await _userRepository.GetProfessorByIdWithUniversityAsync(id);
        if (professor == null) return null;

        // Get assigned course IDs
        var assignedCourseIds = await _professorCourseRepository.GetCourseIdsByProfessorIdAsync(id);

        return new ProfessorDetailViewModel
        {
            Professor = professor,
            UniversityName = professor.University?.Name,
            AssignedCourseIds = assignedCourseIds
        };
    }

    public async Task<ProfessorDetailViewModel?> GetCurrentProfessorAsync(User currentUser)
    {
        if (currentUser.UserType != "professor")
        {
            throw new UnauthorizedAccessException("User is not a professor");
        }

        var professor = await _userRepository.GetByIdWithIncludesAsync(currentUser.UserId);
        if (professor == null) return null;

        return new ProfessorDetailViewModel
        {
            Professor = professor,
            UniversityName = professor.University?.Name
        };
    }

    public async Task<List<int>> GetProfessorCourseIdsAsync(int professorId, User? currentUser)
    {
        // Access control
        if (currentUser != null && currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != professorId)
            {
                throw new UnauthorizedAccessException("You can only view your own courses");
            }
        }

        var courseIds = await _accessControl.GetProfessorCourseIdsAsync(professorId);
        return courseIds.ToList();
    }

    public async Task<ProfessorDetailViewModel> CreateAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string password,
        int universityId,
        bool isAdmin,
        User currentUser)
    {
        // Access control: Only admins can create professors
        if (currentUser.UserType == "professor" && !(currentUser.IsAdmin ?? false))
        {
            throw new UnauthorizedAccessException("Only admins can create professors");
        }

        // Check if university exists
        var university = await _universityRepository.GetByIdAsync(universityId);
        if (university == null)
        {
            throw new KeyNotFoundException("University not found");
        }

        // Check if username or email already exists
        if (await _userRepository.ExistsByUsernameAsync(username))
        {
            throw new InvalidOperationException("Username already exists");
        }

        if (await _userRepository.ExistsByEmailAsync(email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        var professor = new User
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            UserType = "professor",
            UniversityId = universityId,
            IsAdmin = isAdmin,
            IsActive = true
        };

        await _userRepository.AddAsync(professor);

        return new ProfessorDetailViewModel
        {
            Professor = professor,
            UniversityName = university.Name
        };
    }

    public async Task<ProfessorDetailViewModel> UpdateAsync(
        int id,
        string? username,
        string? email,
        string? firstName,
        string? lastName,
        bool? isAdmin,
        bool? isActive,
        User currentUser)
    {
        // Access control: Only admins can update other professors, or professors can update themselves (limited fields)
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                throw new UnauthorizedAccessException("You can only update your own profile");
            }
        }

        var professor = await _userRepository.GetProfessorByIdWithUniversityAsync(id);
        if (professor == null)
        {
            throw new KeyNotFoundException("Professor not found");
        }

        // Determine allowed fields based on permissions
        var isUpdatingSelf = currentUser.UserId == id;
        var isAdminUser = currentUser.UserType == "super_admin" || (currentUser.IsAdmin ?? false);

        // Non-admin professors can only update certain fields about themselves
        if (isUpdatingSelf && !isAdminUser)
        {
            // Only allow first_name, last_name, email
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                professor.FirstName = firstName;
            }

            if (!string.IsNullOrWhiteSpace(lastName))
            {
                professor.LastName = lastName;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (await _userRepository.ExistsByEmailExcludingUserAsync(email, id))
                {
                    throw new InvalidOperationException("Email already exists");
                }
                professor.Email = email;
            }
        }
        else if (isAdminUser)
        {
            // Admins can update all fields
            if (!string.IsNullOrWhiteSpace(username))
            {
                if (await _userRepository.ExistsByUsernameExcludingUserAsync(username, id))
                {
                    throw new InvalidOperationException("Username already exists");
                }
                professor.Username = username;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (await _userRepository.ExistsByEmailExcludingUserAsync(email, id))
                {
                    throw new InvalidOperationException("Email already exists");
                }
                professor.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(firstName))
            {
                professor.FirstName = firstName;
            }

            if (!string.IsNullOrWhiteSpace(lastName))
            {
                professor.LastName = lastName;
            }

            if (isAdmin.HasValue)
            {
                professor.IsAdmin = isAdmin.Value;
            }

            if (isActive.HasValue)
            {
                professor.IsActive = isActive.Value;
            }
        }
        else
        {
            throw new UnauthorizedAccessException("Insufficient permissions to update this professor");
        }

        await _userRepository.UpdateAsync(professor);

        University? university = null;
        if (professor.UniversityId.HasValue)
        {
            university = await _universityRepository.GetByIdAsync(professor.UniversityId.Value);
        }

        return new ProfessorDetailViewModel
        {
            Professor = professor,
            UniversityName = university?.Name
        };
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        // Access control: Only admins can delete professors
        if (currentUser.UserType == "professor" && !(currentUser.IsAdmin ?? false))
        {
            throw new UnauthorizedAccessException("Only admins can delete professors");
        }

        // Prevent self-deletion (except super admins)
        if (currentUser.UserType != "super_admin")
        {
            var currentProfId = currentUser.UserId;
            if (currentProfId == id)
            {
                throw new InvalidOperationException("Cannot delete yourself");
            }
        }

        var professor = await _userRepository.GetProfessorByIdWithUniversityAsync(id);
        if (professor == null)
        {
            throw new KeyNotFoundException("Professor not found");
        }

        await _userRepository.DeleteAsync(professor);
    }

    public async Task ChangePasswordAsync(int id, string newPassword, User currentUser)
    {
        // Access control: Only admins can update other professors' passwords, professors can update their own
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                throw new UnauthorizedAccessException("You can only change your own password");
            }
        }

        var professor = await _userRepository.GetProfessorByIdWithUniversityAsync(id);
        if (professor == null)
        {
            throw new KeyNotFoundException("Professor not found");
        }

        professor.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(professor);
    }
}
