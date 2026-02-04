using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;
    private readonly int[] _platformOwnerUserIds;

    public UserService(
        IUserRepository userRepository,
        IUniversityRepository universityRepository,
        ICourseRepository courseRepository,
        IEmailService emailService,
        IAuditLogService auditLogService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _universityRepository = universityRepository;
        _courseRepository = courseRepository;
        _emailService = emailService;
        _auditLogService = auditLogService;
        _platformOwnerUserIds = configuration.GetSection("Platform:OwnerUserIds").Get<int[]>() ?? Array.Empty<int>();
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
        // Validate user type
        if (!string.IsNullOrWhiteSpace(userType))
        {
            var validUserTypes = new[]
            {
                UserTypes.Student,
                UserTypes.Professor,
                UserTypes.Manager,
                UserTypes.Tutor,
                UserTypes.PlatformCoordinator,
                UserTypes.SuperAdmin
            };

            if (!validUserTypes.Contains(userType))
            {
                throw new ArgumentException($"Invalid user type. Must be one of: {string.Join(", ", validUserTypes)}");
            }
        }

        var (users, total) = await _userRepository.GetPagedAsync(
            userType,
            universityId,
            isAdmin,
            isActive,
            search,
            page,
            pageSize);

        var viewModels = users.Select(u => new UserListViewModel
        {
            User = u,
            UniversityName = u.University?.Name
        }).ToList();

        return (viewModels, total);
    }

    public async Task<UserListViewModel?> GetByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdWithIncludesAsync(id);

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
        if (currentUser.UserType == UserTypes.Manager)
        {
            // Managers can create: Tutor, PlatformCoordinator, Professor, Student (but not Manager or SuperAdmin)
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(userType))
            {
                throw new InvalidOperationException("Managers can only create Tutors, Platform Coordinators, Professors, and Students");
            }

            // Managers can only create users in their own university
            if (universityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only create users in their own university");
            }
        }
        // Legacy: Support old professor with isAdmin flag (will be migrated to Manager)
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            // Treat as Manager for backward compatibility
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(userType))
            {
                throw new InvalidOperationException("Admin professors can only create Tutors, Platform Coordinators, Professors, and Students");
            }

            if (universityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only create users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            // Tutors, Platform Coordinators, and regular Professors cannot create users
            throw new UnauthorizedAccessException("Insufficient permissions to create users");
        }

        // Validate user_type
        var validUserTypes = new[]
        {
            UserTypes.Student,
            UserTypes.Professor,
            UserTypes.Manager,
            UserTypes.Tutor,
            UserTypes.PlatformCoordinator,
            UserTypes.SuperAdmin
        };

        if (!validUserTypes.Contains(userType))
        {
            throw new ArgumentException($"Invalid user_type. Must be one of: {string.Join(", ", validUserTypes)}");
        }

        // Validate university_id for university-scoped roles
        var universityScopedRoles = new[] { UserTypes.Professor, UserTypes.Manager, UserTypes.Tutor, UserTypes.PlatformCoordinator };
        if (universityScopedRoles.Contains(userType) && !universityId.HasValue)
        {
            throw new ArgumentException($"university_id is required for {userType}");
        }

        // Validate course_id for students
        if (userType == UserTypes.Student && courseId.HasValue)
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
        var existingByUsername = await _userRepository.GetByUsernameAsync(username);
        if (existingByUsername != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        var existingByEmail = await _userRepository.GetByEmailAsync(email);
        if (existingByEmail != null)
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Set IsAdmin flag based on role (for backward compatibility with legacy code)
        // Note: IsAdmin is deprecated. Use UserType for role checks instead.
        var isAdminValue = userType switch
        {
            UserTypes.SuperAdmin => true,
            UserTypes.Manager => true,  // Manager is the new "admin professor"
            _ => false
        };

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

        await _userRepository.AddAsync(user);

        // Generate password reset token for email
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var resetToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        user.PasswordResetToken = resetToken;
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);
        await _userRepository.SaveChangesAsync();

        // Send welcome email
        try
        {
            await _emailService.SendWelcomeEmailAsync(
                user.Email,
                user.FirstName,
                user.Username,
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
        var createdUser = await _userRepository.GetByIdWithIncludesAsync(user.UserId);

        // Audit log: User created
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: createdUser?.UniversityId,
            action: "Create",
            entityType: "User",
            entityId: createdUser!.UserId,
            entityName: $"{createdUser.Username} ({createdUser.Email})",
            changes: null);

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
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == UserTypes.Manager)
        {
            // Managers can update: Tutor, PlatformCoordinator, Professor, Student (but not Manager or SuperAdmin)
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Managers can only update Tutors, Platform Coordinators, Professors, and Students");
            }

            // Managers can only update users in their own university
            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only update users in their own university");
            }
        }
        // Legacy: Support old professor with isAdmin flag (will be migrated to Manager)
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            // Treat as Manager for backward compatibility
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Admin professors can only update Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only update users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            // Tutors, Platform Coordinators, and regular Professors cannot update users
            throw new UnauthorizedAccessException("Insufficient permissions to update users");
        }

        // Cannot update yourself
        if (currentUser.UserId == id)
        {
            throw new InvalidOperationException("Cannot update your own account via this endpoint");
        }

        // Track changes for audit log
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>();

        // Check for username conflicts
        if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
        {
            var usernameExists = await _userRepository.ExistsByUsernameExcludingUserAsync(username, id);
            if (usernameExists)
            {
                throw new InvalidOperationException("Username already exists");
            }

            changes["Username"] = (user.Username, username);
            user.Username = username;
        }

        // Check for email conflicts
        if (!string.IsNullOrWhiteSpace(email) && email != user.Email)
        {
            var emailExists = await _userRepository.ExistsByEmailExcludingUserAsync(email, id);
            if (emailExists)
            {
                throw new InvalidOperationException("Email already exists");
            }

            changes["Email"] = (user.Email, email);
            user.Email = email;
        }

        // Update other fields
        if (!string.IsNullOrWhiteSpace(firstName) && user.FirstName != firstName)
        {
            changes["FirstName"] = (user.FirstName, firstName);
            user.FirstName = firstName;
        }

        if (!string.IsNullOrWhiteSpace(lastName) && user.LastName != lastName)
        {
            changes["LastName"] = (user.LastName, lastName);
            user.LastName = lastName;
        }

        if (isAdmin.HasValue && user.IsAdmin != isAdmin.Value)
        {
            changes["IsAdmin"] = (user.IsAdmin, isAdmin.Value);
            user.IsAdmin = isAdmin.Value;
        }

        if (isActive.HasValue && user.IsActive != isActive.Value)
        {
            changes["IsActive"] = (user.IsActive, isActive.Value);
            user.IsActive = isActive.Value;
        }

        if (universityId.HasValue && user.UniversityId != universityId.Value)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university == null)
            {
                throw new KeyNotFoundException("University not found");
            }
            changes["UniversityId"] = (user.UniversityId, universityId.Value);
            user.UniversityId = universityId.Value;
        }

        if (courseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(courseId.Value);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }
            // Note: Course assignment for students should be handled via StudentCourses junction table
            // This is a placeholder for future implementation
        }

        if (!string.IsNullOrWhiteSpace(themePreference) && user.ThemePreference != themePreference)
        {
            changes["ThemePreference"] = (user.ThemePreference, themePreference);
            user.ThemePreference = themePreference;
        }

        if (!string.IsNullOrWhiteSpace(languagePreference) && user.LanguagePreference != languagePreference)
        {
            changes["LanguagePreference"] = (user.LanguagePreference, languagePreference);
            user.LanguagePreference = languagePreference;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        // Reload with includes
        var updatedUser = await _userRepository.GetByIdWithIncludesAsync(id);

        // Audit log: Only log if there were actual changes
        if (changes.Any())
        {
            await _auditLogService.LogAsync(
                userId: currentUser.UserId,
                username: currentUser.Username,
                universityId: updatedUser?.UniversityId,
                action: "Update",
                entityType: "User",
                entityId: updatedUser!.UserId,
                entityName: $"{updatedUser.Username} ({updatedUser.Email})",
                changes: changes);
        }

        return new UserListViewModel
        {
            User = updatedUser!,
            UniversityName = updatedUser?.University?.Name
        };
    }

    public async Task<UserListViewModel> ActivateAsync(int id, User currentUser)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == UserTypes.Manager)
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Managers can only activate Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only activate users in their own university");
            }
        }
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Admin professors can only activate Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only activate users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions");
        }

        var wasActive = user.IsActive;
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        // Reload with includes
        var activatedUser = await _userRepository.GetByIdWithIncludesAsync(id);

        // Audit log: User activated
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>
        {
            ["IsActive"] = (wasActive, true)
        };

        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: activatedUser?.UniversityId,
            action: "Update",
            entityType: "User",
            entityId: activatedUser!.UserId,
            entityName: $"{activatedUser.Username} ({activatedUser.Email})",
            changes: changes);

        return new UserListViewModel
        {
            User = activatedUser!,
            UniversityName = activatedUser?.University?.Name
        };
    }

    public async Task<UserListViewModel> DeactivateAsync(int id, User currentUser)
    {
        var user = await _userRepository.GetByIdAsync(id);

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
        if (currentUser.UserType == UserTypes.Manager)
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Managers can only deactivate Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only deactivate users in their own university");
            }
        }
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Admin professors can only deactivate Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only deactivate users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions");
        }

        var wasActive = user.IsActive;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        // Reload with includes
        var deactivatedUser = await _userRepository.GetByIdWithIncludesAsync(id);

        // Audit log: User deactivated
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>
        {
            ["IsActive"] = (wasActive, false)
        };

        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: deactivatedUser?.UniversityId,
            action: "Update",
            entityType: "User",
            entityId: deactivatedUser!.UserId,
            entityName: $"{deactivatedUser.Username} ({deactivatedUser.Email})",
            changes: changes);

        return new UserListViewModel
        {
            User = deactivatedUser!,
            UniversityName = deactivatedUser?.University?.Name
        };
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Cannot delete yourself
        if (currentUser.UserId == id)
        {
            throw new InvalidOperationException("Cannot delete your own account");
        }

        // Special restriction: Only platform owners can delete other super admins
        if (user.UserType == UserTypes.SuperAdmin)
        {
            if (!_platformOwnerUserIds.Contains(currentUser.UserId))
            {
                throw new InvalidOperationException("Only platform owners can delete super admin accounts");
            }
        }

        // Permission checks
        if (currentUser.UserType == UserTypes.Manager)
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Managers can only delete Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only delete users in their own university");
            }
        }
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Admin professors can only delete Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only delete users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions");
        }

        // Capture user info for audit log before deletion
        var deletedUsername = user.Username;
        var deletedEmail = user.Email;
        var deletedUniversityId = user.UniversityId;

        await _userRepository.DeleteAsync(user);

        // Audit log: User deleted
        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: deletedUniversityId,
            action: "Delete",
            entityType: "User",
            entityId: id,
            entityName: $"{deletedUsername} ({deletedEmail})",
            changes: null);
    }

    public async Task ChangePasswordAsync(int id, string newPassword, User currentUser)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Permission checks
        if (currentUser.UserType == UserTypes.Manager)
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Managers can only change passwords for Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Managers can only change passwords for users in their own university");
            }
        }
        else if (currentUser.UserType == UserTypes.Professor && (currentUser.IsAdmin ?? false))
        {
            var allowedTypes = new[] { UserTypes.Tutor, UserTypes.PlatformCoordinator, UserTypes.Professor, UserTypes.Student };
            if (!allowedTypes.Contains(user.UserType))
            {
                throw new InvalidOperationException("Admin professors can only change passwords for Tutors, Platform Coordinators, Professors, and Students");
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                throw new InvalidOperationException("Admin professors can only change passwords for users in their own university");
            }
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions");
        }

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        // Audit log: Password changed (don't include actual password)
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>
        {
            ["Password"] = ("***", "***")
        };

        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: user.UniversityId,
            action: "Update",
            entityType: "User",
            entityId: user.UserId,
            entityName: $"{user.Username} ({user.Email})",
            changes: changes);
    }
}
