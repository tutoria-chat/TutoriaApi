using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Helpers;
using TutoriaApi.Web.Management.DTOs;
using BCrypt.Net;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/professors")]
[Authorize(Policy = "ProfessorOrAbove")]
public class ProfessorsController : ControllerBase
{
    private readonly IUniversityRepository _universityRepository;
    private readonly TutoriaDbContext _context;
    private readonly AccessControlHelper _accessControl;
    private readonly ILogger<ProfessorsController> _logger;

    public ProfessorsController(
        IUniversityRepository universityRepository,
        TutoriaDbContext context,
        AccessControlHelper accessControl,
        ILogger<ProfessorsController> logger)
    {
        _universityRepository = universityRepository;
        _context = context;
        _accessControl = accessControl;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.Users.FindAsync(userId);
    }

    private bool IsSuperAdmin()
    {
        return User.IsInRole("super_admin");
    }

    private async Task<bool> IsAdminProfessorAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        return currentUser?.UserType == "professor" && (currentUser.IsAdmin ?? false);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProfessorDto>>> GetProfessors(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? universityId = null,
        [FromQuery] bool? isAdmin = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        // Access control: Only admins can see all professors
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                return Forbid(); // Non-admin professors cannot list professors
            }

            // Admin professors can only see professors from their own university
            if (!universityId.HasValue && currentUser.UniversityId.HasValue)
            {
                universityId = currentUser.UniversityId.Value;
            }
        }

        var query = _context.Users
            .Where(u => u.UserType == "professor")
            .Include(u => u.University)
            .AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(u => u.UniversityId == universityId.Value);
        }

        if (isAdmin.HasValue)
        {
            query = query.Where(u => u.IsAdmin == isAdmin.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var professors = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = professors.Select(u => new ProfessorDto
        {
            Id = u.UserId,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            IsAdmin = u.IsAdmin ?? false,
            IsActive = u.IsActive,
            UniversityId = u.UniversityId,
            UniversityName = u.University?.Name,
            ThemePreference = u.ThemePreference ?? "system",
            LanguagePreference = u.LanguagePreference ?? "pt-br",
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return Ok(new PaginatedResponse<ProfessorDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfessorDto>> GetCurrentProfessor()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null || currentUser.UserType != "professor")
        {
            return Forbid();
        }

        var professor = await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        return Ok(new ProfessorDto
        {
            Id = professor.UserId,
            Username = professor.Username,
            Email = professor.Email,
            FirstName = professor.FirstName,
            LastName = professor.LastName,
            IsAdmin = professor.IsAdmin ?? false,
            IsActive = professor.IsActive,
            UniversityId = professor.UniversityId,
            UniversityName = professor.University?.Name,
            ThemePreference = professor.ThemePreference ?? "system",
            LanguagePreference = professor.LanguagePreference ?? "pt-br",
            LastLoginAt = professor.LastLoginAt,
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProfessorDto>> GetProfessor(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control: Only admins can view other professors
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                return Forbid();
            }
        }

        var professor = await _context.Users
            .Where(u => u.UserType == "professor")
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        return Ok(new ProfessorDto
        {
            Id = professor.UserId,
            Username = professor.Username,
            Email = professor.Email,
            FirstName = professor.FirstName,
            LastName = professor.LastName,
            IsAdmin = professor.IsAdmin ?? false,
            IsActive = professor.IsActive,
            UniversityId = professor.UniversityId,
            UniversityName = professor.University?.Name,
            ThemePreference = professor.ThemePreference ?? "system",
            LanguagePreference = professor.LanguagePreference ?? "pt-br",
            LastLoginAt = professor.LastLoginAt,
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpGet("{id}/courses")]
    public async Task<ActionResult<object>> GetProfessorCourses(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                return Forbid();
            }
        }

        var courseIds = await _accessControl.GetProfessorCourseIdsAsync(id);

        return Ok(new { course_ids = courseIds });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ProfessorDto>> CreateProfessor([FromBody] ProfessorCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control: Only admins can create professors
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                return Forbid();
            }
        }

        // Check if university exists
        var university = await _universityRepository.GetByIdAsync(request.UniversityId);
        if (university == null)
        {
            return NotFound(new { message = "University not found" });
        }

        // Check if username or email already exists (across all users)
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

        var professor = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password),
            UserType = "professor",
            UniversityId = request.UniversityId,
            IsAdmin = request.IsAdmin,
            IsActive = true
        };

        _context.Users.Add(professor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created professor {Username} with ID {Id}", professor.Username, professor.UserId);

        return CreatedAtAction(nameof(GetProfessor), new { id = professor.UserId }, new ProfessorDto
        {
            Id = professor.UserId,
            Username = professor.Username,
            Email = professor.Email,
            FirstName = professor.FirstName,
            LastName = professor.LastName,
            IsAdmin = professor.IsAdmin ?? false,
            IsActive = professor.IsActive,
            UniversityId = professor.UniversityId,
            UniversityName = university.Name,
            ThemePreference = professor.ThemePreference ?? "system",
            LanguagePreference = professor.LanguagePreference ?? "pt-br",
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProfessorDto>> UpdateProfessor(int id, [FromBody] ProfessorUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control: Only admins can update other professors, or professors can update themselves (limited fields)
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                return Forbid();
            }
        }

        var professor = await _context.Users
            .Where(u => u.UserType == "professor")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        // Determine allowed fields based on permissions
        var isUpdatingSelf = currentUser.UserId == id;
        var isAdmin = currentUser.UserType == "super_admin" || (currentUser.IsAdmin ?? false);

        // Non-admin professors can only update certain fields about themselves
        if (isUpdatingSelf && !isAdmin)
        {
            // Only allow first_name, last_name, email
            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                professor.FirstName = request.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                professor.LastName = request.LastName;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingByEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != id);

                if (existingByEmail != null)
                {
                    return BadRequest(new { message = "Email already exists" });
                }

                professor.Email = request.Email;
            }
        }
        else if (isAdmin)
        {
            // Admins can update all fields
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                var existingByUsername = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.UserId != id);

                if (existingByUsername != null)
                {
                    return BadRequest(new { message = "Username already exists" });
                }

                professor.Username = request.Username;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingByEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != id);

                if (existingByEmail != null)
                {
                    return BadRequest(new { message = "Email already exists" });
                }

                professor.Email = request.Email;
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                professor.FirstName = request.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                professor.LastName = request.LastName;
            }

            if (request.IsAdmin.HasValue)
            {
                professor.IsAdmin = request.IsAdmin.Value;
            }

            if (request.IsActive.HasValue)
            {
                professor.IsActive = request.IsActive.Value;
            }
        }
        else
        {
            return Forbid();
        }

        professor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated professor {Username} with ID {Id}", professor.Username, professor.UserId);

        University? university = null;
        if (professor.UniversityId.HasValue)
        {
            university = await _universityRepository.GetByIdAsync(professor.UniversityId.Value);
        }

        return Ok(new ProfessorDto
        {
            Id = professor.UserId,
            Username = professor.Username,
            Email = professor.Email,
            FirstName = professor.FirstName,
            LastName = professor.LastName,
            IsAdmin = professor.IsAdmin ?? false,
            IsActive = professor.IsActive,
            UniversityId = professor.UniversityId,
            UniversityName = university?.Name,
            ThemePreference = professor.ThemePreference ?? "system",
            LanguagePreference = professor.LanguagePreference ?? "pt-br",
            LastLoginAt = professor.LastLoginAt,
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> DeleteProfessor(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control: Only admins can delete professors
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                return Forbid();
            }
        }

        // Prevent self-deletion
        if (currentUser.UserType != "super_admin")
        {
            var currentProfId = currentUser.UserId;
            if (currentProfId == id)
            {
                return BadRequest(new { message = "Cannot delete yourself" });
            }
        }

        var professor = await _context.Users
            .Where(u => u.UserType == "professor")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        _context.Users.Remove(professor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted professor {Username} with ID {Id}", professor.Username, professor.UserId);

        return Ok(new { message = "Professor deleted successfully" });
    }

    [HttpPut("{id}/password")]
    public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Access control: Only admins can update other professors' passwords, professors can update their own
        if (currentUser.UserType == "professor")
        {
            var currentProfId = currentUser.UserId;
            if (!(currentUser.IsAdmin ?? false) && currentProfId != id)
            {
                return Forbid();
            }
        }

        var professor = await _context.Users
            .Where(u => u.UserType == "professor")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        professor.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        professor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Changed password for professor {Username} with ID {Id}", professor.Username, professor.UserId);

        return Ok(new { message = "Password changed successfully" });
    }
}
