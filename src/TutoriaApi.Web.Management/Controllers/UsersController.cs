using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;
using BCrypt.Net;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOrAbove")] // Require AdminOrAbove for all user operations
public class UsersController : ControllerBase
{
    private readonly IUniversityRepository _universityRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUniversityRepository universityRepository,
        ICourseRepository courseRepository,
        TutoriaDbContext context,
        ILogger<UsersController> logger)
    {
        _universityRepository = universityRepository;
        _courseRepository = courseRepository;
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private string GetCurrentUserType()
    {
        return User.FindFirst("type")?.Value ?? throw new UnauthorizedAccessException("Invalid user type in token");
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? userType = null,
        [FromQuery] int? universityId = null,
        [FromQuery] bool? isAdmin = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .AsQueryable();

        // Filter by user type
        if (!string.IsNullOrWhiteSpace(userType))
        {
            if (userType != "student" && userType != "professor" && userType != "super_admin")
            {
                return BadRequest(new { message = "Invalid user type. Must be: student, professor, or super_admin" });
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
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = users.Select(u => new UserDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            UserType = u.UserType,
            IsActive = u.IsActive,
            IsAdmin = u.IsAdmin,
            UniversityId = u.UniversityId,
            UniversityName = u.University?.Name,
            CourseId = u.CourseId,
            CourseName = u.Course?.Name,
            ThemePreference = u.ThemePreference ?? "system",
            LanguagePreference = u.LanguagePreference ?? "pt-br",
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return Ok(new PaginatedResponse<UserDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            UniversityId = user.UniversityId,
            UniversityName = user.University?.Name,
            CourseId = user.CourseId,
            CourseName = user.Course?.Name,
            ThemePreference = user.ThemePreference ?? "system",
            LanguagePreference = user.LanguagePreference ?? "pt-br",
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserType = GetCurrentUserType();
        var currentUserId = GetCurrentUserId();

        // Permission checks based on Python API logic
        if (currentUserType == "professor")
        {
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || !currentUser.IsAdmin.GetValueOrDefault(false))
            {
                return Forbid(); // Only admin professors can create users
            }

            // Admin professors can only create regular (non-admin) professors
            if (request.UserType != "professor" || request.IsAdmin)
            {
                return StatusCode(403, new { message = "Admin professors can only create regular (non-admin) professors" });
            }

            // Admin professors can only create professors in their own university
            if (request.UniversityId != currentUser.UniversityId)
            {
                return StatusCode(403, new { message = "Admin professors can only create professors in their own university" });
            }
        }

        // Validate user_type
        if (request.UserType != "student" && request.UserType != "professor" && request.UserType != "super_admin")
        {
            return BadRequest(new { message = "Invalid user_type. Must be: student, professor, or super_admin" });
        }

        // Validate university_id for professors
        if (request.UserType == "professor" && !request.UniversityId.HasValue)
        {
            return BadRequest(new { message = "university_id is required for professors" });
        }

        // Validate course_id for students
        if (request.UserType == "student" && request.CourseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(request.CourseId.Value);
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }
        }

        // Check if university exists (for professors)
        if (request.UniversityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(request.UniversityId.Value);
            if (university == null)
            {
                return NotFound(new { message = "University not found" });
            }
        }

        // Check if username or email already exists
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingByUsername != null)
        {
            return BadRequest(new { message = "Username already exists" });
        }

        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingByEmail != null)
        {
            return BadRequest(new { message = "Email already exists" });
        }

        // Super admins must have is_admin=True
        var isAdminValue = request.IsAdmin;
        if (request.UserType == "super_admin")
        {
            isAdminValue = true;
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password),
            UserType = request.UserType,
            UniversityId = request.UniversityId,
            CourseId = request.CourseId,
            IsAdmin = isAdminValue,
            IsActive = true,
            ThemePreference = request.ThemePreference ?? "system",
            LanguagePreference = request.LanguagePreference ?? "pt-br"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created user {Username} ({UserType}) with ID {Id}", user.Username, user.UserType, user.UserId);

        // Reload with includes for response
        var createdUser = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == user.UserId);

        return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            UniversityId = user.UniversityId,
            UniversityName = createdUser?.University?.Name,
            CourseId = user.CourseId,
            CourseName = createdUser?.Course?.Name,
            ThemePreference = user.ThemePreference ?? "system",
            LanguagePreference = user.LanguagePreference ?? "pt-br",
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UserUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserType = GetCurrentUserType();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Permission checks (similar to Python API)
        if (currentUserType == "professor")
        {
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || !currentUser.IsAdmin.GetValueOrDefault(false))
            {
                return Forbid();
            }

            // Admin professors can only update regular professors
            if (user.UserType != "professor" || user.IsAdmin.GetValueOrDefault(false))
            {
                return StatusCode(403, new { message = "Admin professors can only update regular professors" });
            }

            // Admin professors can only update professors in their own university
            if (user.UniversityId != currentUser.UniversityId)
            {
                return StatusCode(403, new { message = "Admin professors can only update professors in their own university" });
            }
        }

        // Cannot update yourself (use /auth/me for that)
        if (currentUserId == id)
        {
            return BadRequest(new { message = "Cannot update your own account via this endpoint. Use /auth/me instead" });
        }

        // Check for username conflicts
        if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
        {
            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.UserId != id);

            if (existingByUsername != null)
            {
                return BadRequest(new { message = "Username already exists" });
            }

            user.Username = request.Username;
        }

        // Check for email conflicts
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != id);

            if (existingByEmail != null)
            {
                return BadRequest(new { message = "Email already exists" });
            }

            user.Email = request.Email;
        }

        // Update other fields
        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName;
        }

        if (request.IsAdmin.HasValue)
        {
            user.IsAdmin = request.IsAdmin.Value;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        if (request.UniversityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(request.UniversityId.Value);
            if (university == null)
            {
                return NotFound(new { message = "University not found" });
            }
            user.UniversityId = request.UniversityId.Value;
        }

        if (request.CourseId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(request.CourseId.Value);
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }
            user.CourseId = request.CourseId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ThemePreference))
        {
            user.ThemePreference = request.ThemePreference;
        }

        if (!string.IsNullOrWhiteSpace(request.LanguagePreference))
        {
            user.LanguagePreference = request.LanguagePreference;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated user {Username} ({UserType}) with ID {Id}", user.Username, user.UserType, user.UserId);

        // Reload with includes
        var updatedUser = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return Ok(new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            UniversityId = user.UniversityId,
            UniversityName = updatedUser?.University?.Name,
            CourseId = user.CourseId,
            CourseName = updatedUser?.Course?.Name,
            ThemePreference = user.ThemePreference ?? "system",
            LanguagePreference = user.LanguagePreference ?? "pt-br",
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UserDto>> ActivateUser(int id)
    {
        var currentUserType = GetCurrentUserType();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Permission checks
        if (currentUserType == "professor")
        {
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || !currentUser.IsAdmin.GetValueOrDefault(false))
            {
                return Forbid();
            }

            // Admin professors can only activate regular professors in their university
            if (user.UserType != "professor" || user.IsAdmin.GetValueOrDefault(false))
            {
                return StatusCode(403, new { message = "Admin professors can only activate regular professors" });
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                return StatusCode(403, new { message = "Admin professors can only activate professors in their own university" });
            }
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activated user {Username} ({UserType}) with ID {Id}", user.Username, user.UserType, user.UserId);

        // Reload with includes
        var activatedUser = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return Ok(new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            UniversityId = user.UniversityId,
            UniversityName = activatedUser?.University?.Name,
            CourseId = user.CourseId,
            CourseName = activatedUser?.Course?.Name,
            ThemePreference = user.ThemePreference ?? "system",
            LanguagePreference = user.LanguagePreference ?? "pt-br",
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    [HttpPatch("{id}/deactivate")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UserDto>> DeactivateUser(int id)
    {
        var currentUserType = GetCurrentUserType();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Cannot deactivate yourself
        if (currentUserId == id)
        {
            return BadRequest(new { message = "Cannot deactivate your own account" });
        }

        // Permission checks
        if (currentUserType == "professor")
        {
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || !currentUser.IsAdmin.GetValueOrDefault(false))
            {
                return Forbid();
            }

            // Admin professors can only deactivate regular professors in their university
            if (user.UserType != "professor" || user.IsAdmin.GetValueOrDefault(false))
            {
                return StatusCode(403, new { message = "Admin professors can only deactivate regular professors" });
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                return StatusCode(403, new { message = "Admin professors can only deactivate professors in their own university" });
            }
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated user {Username} ({UserType}) with ID {Id}", user.Username, user.UserType, user.UserId);

        // Reload with includes
        var deactivatedUser = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return Ok(new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            UniversityId = user.UniversityId,
            UniversityName = deactivatedUser?.University?.Name,
            CourseId = user.CourseId,
            CourseName = deactivatedUser?.Course?.Name,
            ThemePreference = user.ThemePreference ?? "system",
            LanguagePreference = user.LanguagePreference ?? "pt-br",
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")] // Only super admins can hard delete
    public async Task<ActionResult> DeleteUser(int id)
    {
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Cannot delete yourself
        if (currentUserId == id)
        {
            return BadRequest(new { message = "Cannot delete your own account" });
        }

        var username = user.Username;
        var userType = user.UserType;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Permanently deleted user {Username} ({UserType}) with ID {Id}", username, userType, id);

        return Ok(new { message = $"User {username} ({userType}) permanently deleted", userId = id, deleted = true });
    }

    [HttpPut("{id}/password")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> ChangeUserPassword(int id, [FromBody] ChangeUserPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserType = GetCurrentUserType();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Permission checks
        if (currentUserType == "professor")
        {
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || !currentUser.IsAdmin.GetValueOrDefault(false))
            {
                return Forbid();
            }

            // Admin professors can only change passwords for regular professors in their university
            if (user.UserType != "professor" || user.IsAdmin.GetValueOrDefault(false))
            {
                return StatusCode(403, new { message = "Admin professors can only change passwords for regular professors" });
            }

            if (user.UniversityId != currentUser.UniversityId)
            {
                return StatusCode(403, new { message = "Admin professors can only change passwords for professors in their own university" });
            }
        }

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Changed password for user {Username} ({UserType}) with ID {Id}", user.Username, user.UserType, user.UserId);

        return Ok(new { message = "Password changed successfully" });
    }
}
