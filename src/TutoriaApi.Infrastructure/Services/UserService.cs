using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly TutoriaDbContext _context;
    private readonly IUniversityRepository _universityRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IEmailService _emailService;

    public UserService(
        TutoriaDbContext context,
        IUniversityRepository universityRepository,
        ICourseRepository courseRepository,
        IEmailService emailService)
    {
        _context = context;
        _universityRepository = universityRepository;
        _courseRepository = courseRepository;
        _emailService = emailService;
    }

    public async Task<(List<UserListViewModel> Items, int Total)> GetPagedAsync(
        string? userType,
        int? universityId,
        bool? isAdmin,
        bool? isActive,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Users
            .Include(u => u.University)
            .AsQueryable();

        // Filter by user type
        if (!string.IsNullOrWhiteSpace(userType))
        {
            if (userType != "student" && userType != "professor" && userType != "super_admin")
            {
                throw new ArgumentException("Invalid user type. Must be: student, professor, or super_admin");
            }
            query = query.Where(u => u.UserType == userType);
        }

        // Filter by university
        if (universityId.HasValue)
        {
            query = query.Where(u => u.UniversityId == universityId.Value);
        }

        // Filter by isAdmin
        if (isAdmin.HasValue)
        {
            query = query.Where(u => u.IsAdmin == isAdmin.Value);
        }

        // Filter by isActive
        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewModels = users.Select(u => new UserListViewModel
        {
            User = u,
            UniversityName = u.University?.Name
        }).ToList();

        return (viewModels, total);
    }

    public async Task<UserListViewModel?> GetByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return null;

        return new UserListViewModel
        {
            User = user,
            UniversityName = user.University?.Name
        };
    }

    public async Task<UserListViewModel> CreateAsync(
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
        User currentUser)
    {
        // Permission checks based on current user
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Only admin professors can create users");
            }

            // Admin professors can only create regular (non-admin) professors
            if (userType != "professor" || isAdmin)
            {
                throw new InvalidOperationException("Admin professors can only create regular (non-admin) professors");
            }

            // Admin professors can only create professors in their own university
            if (universityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only create professors in their own university");
            }
        }

        // Validate user_type
        if (userType != "student" && userType != "professor" && userType != "super_admin")
        {
            throw new ArgumentException("Invalid user_type. Must be: student, professor, or super_admin");
        }

        // Validate university_id for professors
        if (userType == "professor" && !universityId.HasValue)
        {
            throw new ArgumentException("university_id is required for professors");
        }

        // Validate course_id for students
        if (userType == "student" && courseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(courseId.Value);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }
        }

        // Check if university exists (for professors)
        if (universityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university == null)
            {
                throw new KeyNotFoundException("University not found");
            }
        }

        // Check if username or email already exists
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (existingByUsername != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingByEmail != null)
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Super admins must have is_admin=True
        var isAdminValue = isAdmin;
        if (userType == "super_admin")
        {
            isAdminValue = true;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            UserType = userType,
            UniversityId = universityId,
            IsAdmin = isAdminValue,
            IsActive = true,
            ThemePreference = themePreference ?? "system",
            LanguagePreference = languagePreference ?? "pt-br"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate password reset token for email
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var resetToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        user.PasswordResetToken = resetToken;
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);
        await _context.SaveChangesAsync();

        // Send welcome email
        try
        {
            await _emailService.SendWelcomeEmailAsync(
                user.Email,
                user.FirstName,
                user.Username,
                password,
                resetToken,
                user.UserType,
                user.LanguagePreference ?? "en"
            );
        }
        catch
        {
            // Continue - user is created, email failure shouldn't block the operation
        }

        // Reload with includes
        var createdUser = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == user.UserId);

        return new UserListViewModel
        {
            User = createdUser!,
            UniversityName = createdUser?.University?.Name
        };
    }

    public async Task<UserListViewModel> UpdateAsync(
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
        User currentUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Insufficient permissions");
            }

            // Admin professors can only update regular professors
            if (user.UserType != "professor" || (user.IsAdmin ?? false))
            {
                throw new InvalidOperationException("Admin professors can only update regular professors");
            }

            // Admin professors can only update professors in their own university
            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only update professors in their own university");
            }
        }

        // Cannot update yourself
        if (currentUser.UserId == id)
        {
            throw new InvalidOperationException("Cannot update your own account via this endpoint. Use /auth/me instead");
        }

        // Check for username conflicts
        if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
        {
            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.UserId != id);

            if (existingByUsername != null)
            {
                throw new InvalidOperationException("Username already exists");
            }

            user.Username = username;
        }

        // Check for email conflicts
        if (!string.IsNullOrWhiteSpace(email) && email != user.Email)
        {
            var existingByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.UserId != id);

            if (existingByEmail != null)
            {
                throw new InvalidOperationException("Email already exists");
            }

            user.Email = email;
        }

        // Update other fields
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            user.FirstName = firstName;
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            user.LastName = lastName;
        }

        if (isAdmin.HasValue)
        {
            user.IsAdmin = isAdmin.Value;
        }

        if (isActive.HasValue)
        {
            user.IsActive = isActive.Value;
        }

        if (universityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university == null)
            {
                throw new KeyNotFoundException("University not found");
            }
            user.UniversityId = universityId.Value;
        }

        if (courseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(courseId.Value);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }
            // TODO: Handle course assignment via StudentCourses junction table
        }

        if (!string.IsNullOrWhiteSpace(themePreference))
        {
            user.ThemePreference = themePreference;
        }

        if (!string.IsNullOrWhiteSpace(languagePreference))
        {
            user.LanguagePreference = languagePreference;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload with includes
        var updatedUser = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return new UserListViewModel
        {
            User = updatedUser!,
            UniversityName = updatedUser?.University?.Name
        };
    }

    public async Task<UserListViewModel> ActivateAsync(int id, User currentUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Insufficient permissions");
            }

            // Admin professors can only activate regular professors in their university
            if (user.UserType != "professor" || (user.IsAdmin ?? false))
            {
                throw new InvalidOperationException("Admin professors can only activate regular professors");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only activate professors in their own university");
            }
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload with includes
        var activatedUser = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return new UserListViewModel
        {
            User = activatedUser!,
            UniversityName = activatedUser?.University?.Name
        };
    }

    public async Task<UserListViewModel> DeactivateAsync(int id, User currentUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Cannot deactivate yourself
        if (currentUser.UserId == id)
        {
            throw new InvalidOperationException("Cannot deactivate your own account");
        }

        // Permission checks
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Insufficient permissions");
            }

            // Admin professors can only deactivate regular professors in their university
            if (user.UserType != "professor" || (user.IsAdmin ?? false))
            {
                throw new InvalidOperationException("Admin professors can only deactivate regular professors");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only deactivate professors in their own university");
            }
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload with includes
        var deactivatedUser = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return new UserListViewModel
        {
            User = deactivatedUser!,
            UniversityName = deactivatedUser?.University?.Name
        };
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Cannot delete yourself
        if (currentUser.UserId == id)
        {
            throw new InvalidOperationException("Cannot delete your own account");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(int id, string newPassword, User currentUser)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                throw new UnauthorizedAccessException("Insufficient permissions");
            }

            // Admin professors can only change passwords for regular professors in their university
            if (user.UserType != "professor" || (user.IsAdmin ?? false))
            {
                throw new InvalidOperationException("Admin professors can only change passwords for regular professors");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only change passwords for professors in their own university");
            }
        }

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
