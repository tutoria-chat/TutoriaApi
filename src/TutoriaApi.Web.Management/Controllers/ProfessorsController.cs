using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;
using BCrypt.Net;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/professors")]
[Authorize(Policy = "AdminOrAbove")] // Require AdminOrAbove for all professor operations
public class ProfessorsController : ControllerBase
{
    private readonly IUniversityRepository _universityRepository;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<ProfessorsController> _logger;

    public ProfessorsController(
        IUniversityRepository universityRepository,
        TutoriaDbContext context,
        ILogger<ProfessorsController> logger)
    {
        _universityRepository = universityRepository;
        _context = context;
        _logger = logger;
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

    [HttpGet("{id}")]
    public async Task<ActionResult<ProfessorDto>> GetProfessor(int id)
    {
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
            LastLoginAt = professor.LastLoginAt,
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<ProfessorDto>> CreateProfessor([FromBody] ProfessorCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
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
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<ProfessorDto>> UpdateProfessor(int id, [FromBody] ProfessorUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var professor = await _context.Users
            .Where(u => u.UserType == "professor")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

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
            LastLoginAt = professor.LastLoginAt,
            CreatedAt = professor.CreatedAt,
            UpdatedAt = professor.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> DeleteProfessor(int id)
    {
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
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
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
