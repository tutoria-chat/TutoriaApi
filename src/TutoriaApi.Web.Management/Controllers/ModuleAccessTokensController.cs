using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages access tokens for module widgets and external integrations.
/// </summary>
/// <remarks>
/// Module Access Tokens provide secure, scoped access to specific modules for embedding
/// AI tutors in external applications (e.g., LMS widgets, mobile apps, websites).
///
/// **Authorization**: All endpoints require ProfessorOrAbove policy.
///
/// **Token Features**:
/// - Secure random generation (32 bytes, URL-safe Base64)
/// - Configurable expiration dates
/// - Permission scopes: AllowChat, AllowFileAccess
/// - Usage tracking: UsageCount, LastUsedAt
/// - Active/inactive status for easy revocation
///
/// **Use Cases**:
/// - Embed AI tutor widgets in external LMS (Canvas, Moodle)
/// - Provide API access to mobile applications
/// - Create temporary guest access for specific modules
/// </remarks>
[ApiController]
[Route("api/module-access-tokens")]
[Authorize(Policy = "ProfessorOrAbove")]
public class ModuleAccessTokensController : ControllerBase
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<ModuleAccessTokensController> _logger;

    public ModuleAccessTokensController(
        TutoriaDbContext context,
        ILogger<ModuleAccessTokensController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ModuleAccessTokenListDto>>> GetModuleAccessTokens(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? moduleId = null,
        [FromQuery] int? universityId = null,
        [FromQuery] bool? isActive = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = _context.ModuleAccessTokens
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

        // Access control based on user type
        var userType = User.FindFirst("type")?.Value;
        var isAdmin = User.FindFirst("isAdmin")?.Value?.ToLower() == "true";
        var userIdClaim = User.FindFirst("user_id")?.Value;

        if (userType == "professor" && !isAdmin && int.TryParse(userIdClaim, out var professorId))
        {
            // Non-admin professors can only see tokens for modules in courses they're assigned to
            query = query.Where(t => _context.ProfessorCourses
                .Any(pc => pc.ProfessorId == professorId && pc.CourseId == t.Module.CourseId));
        }
        else if (userType == "professor" && isAdmin)
        {
            // Admin professors can see tokens for modules in their university
            var universityIdClaim = User.FindFirst("university_id")?.Value;
            if (int.TryParse(universityIdClaim, out var profUniversityId))
            {
                query = query.Where(t => t.Module.Course.UniversityId == profUniversityId);
            }
        }
        // Super admins can see all tokens (no additional filtering)

        // Apply filters
        if (universityId.HasValue)
        {
            query = query.Where(t => t.Module.Course.UniversityId == universityId.Value);
        }

        if (moduleId.HasValue)
        {
            query = query.Where(t => t.ModuleId == moduleId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        var total = await query.CountAsync();
        var tokens = await query
            .OrderBy(t => t.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = tokens.Select(t => new ModuleAccessTokenListDto
        {
            Id = t.Id,
            Token = t.Token,
            Name = t.Name,
            Description = t.Description,
            ModuleId = t.ModuleId,
            ModuleName = t.Module?.Name,
            IsActive = t.IsActive,
            ExpiresAt = t.ExpiresAt,
            AllowChat = t.AllowChat,
            AllowFileAccess = t.AllowFileAccess,
            UsageCount = t.UsageCount,
            LastUsedAt = t.LastUsedAt,
            CreatedAt = t.CreatedAt
        }).ToList();

        return Ok(new PaginatedResponse<ModuleAccessTokenListDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModuleAccessTokenDetailDto>> GetModuleAccessToken(int id)
    {
        var token = await _context.ModuleAccessTokens
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (token == null)
        {
            return NotFound(new { message = "Module access token not found" });
        }

        return Ok(new ModuleAccessTokenDetailDto
        {
            Id = token.Id,
            Token = token.Token,
            Name = token.Name,
            Description = token.Description,
            ModuleId = token.ModuleId,
            ModuleName = token.Module?.Name,
            CourseId = token.Module?.CourseId,
            CourseName = token.Module?.Course?.Name,
            UniversityId = token.Module?.Course?.UniversityId,
            UniversityName = token.Module?.Course?.University?.Name,
            CreatedByProfessorId = token.CreatedByProfessorId,
            CreatedByName = token.CreatedBy != null ? $"{token.CreatedBy.FirstName} {token.CreatedBy.LastName}" : null,
            IsActive = token.IsActive,
            ExpiresAt = token.ExpiresAt,
            AllowChat = token.AllowChat,
            AllowFileAccess = token.AllowFileAccess,
            UsageCount = token.UsageCount,
            LastUsedAt = token.LastUsedAt,
            CreatedAt = token.CreatedAt,
            UpdatedAt = token.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<ModuleAccessTokenDetailDto>> CreateModuleAccessToken(
        [FromBody] ModuleAccessTokenCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Verify module exists
        var module = await _context.Modules
            .Include(m => m.Course)
                .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(m => m.Id == request.ModuleId);

        if (module == null)
        {
            return NotFound(new { message = "Module not found" });
        }

        // Generate secure random token (64 characters)
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var generatedToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        // Calculate expiration date
        DateTime? expiresAt = null;
        if (request.ExpiresInDays.HasValue)
        {
            expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays.Value);
        }

        // Get current user ID from JWT (if needed for CreatedByProfessorId)
        // For now, leaving it null - would need to extract from JWT claims
        int? createdByProfessorId = null;

        var token = new ModuleAccessToken
        {
            Token = generatedToken,
            Name = request.Name,
            Description = request.Description,
            ModuleId = request.ModuleId,
            CreatedByProfessorId = createdByProfessorId,
            IsActive = true,
            ExpiresAt = expiresAt,
            AllowChat = request.AllowChat,
            AllowFileAccess = request.AllowFileAccess,
            UsageCount = 0
        };

        _context.ModuleAccessTokens.Add(token);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created module access token {Name} for module {ModuleId}", token.Name, token.ModuleId);

        return CreatedAtAction(nameof(GetModuleAccessToken), new { id = token.Id }, new ModuleAccessTokenDetailDto
        {
            Id = token.Id,
            Token = token.Token,
            Name = token.Name,
            Description = token.Description,
            ModuleId = token.ModuleId,
            ModuleName = module.Name,
            CourseId = module.CourseId,
            CourseName = module.Course?.Name,
            UniversityId = module.Course?.UniversityId,
            UniversityName = module.Course?.University?.Name,
            CreatedByProfessorId = token.CreatedByProfessorId,
            IsActive = token.IsActive,
            ExpiresAt = token.ExpiresAt,
            AllowChat = token.AllowChat,
            AllowFileAccess = token.AllowFileAccess,
            UsageCount = token.UsageCount,
            LastUsedAt = token.LastUsedAt,
            CreatedAt = token.CreatedAt,
            UpdatedAt = token.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ModuleAccessTokenDetailDto>> UpdateModuleAccessToken(
        int id,
        [FromBody] ModuleAccessTokenUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var token = await _context.ModuleAccessTokens
            .Include(t => t.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (token == null)
        {
            return NotFound(new { message = "Module access token not found" });
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            token.Name = request.Name;
        }

        if (request.Description != null)
        {
            token.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            token.IsActive = request.IsActive.Value;
        }

        if (request.AllowChat.HasValue)
        {
            token.AllowChat = request.AllowChat.Value;
        }

        if (request.AllowFileAccess.HasValue)
        {
            token.AllowFileAccess = request.AllowFileAccess.Value;
        }

        token.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated module access token {Name} with ID {Id}", token.Name, token.Id);

        return Ok(new ModuleAccessTokenDetailDto
        {
            Id = token.Id,
            Token = token.Token,
            Name = token.Name,
            Description = token.Description,
            ModuleId = token.ModuleId,
            ModuleName = token.Module?.Name,
            CourseId = token.Module?.CourseId,
            CourseName = token.Module?.Course?.Name,
            UniversityId = token.Module?.Course?.UniversityId,
            UniversityName = token.Module?.Course?.University?.Name,
            CreatedByProfessorId = token.CreatedByProfessorId,
            CreatedByName = token.CreatedBy != null ? $"{token.CreatedBy.FirstName} {token.CreatedBy.LastName}" : null,
            IsActive = token.IsActive,
            ExpiresAt = token.ExpiresAt,
            AllowChat = token.AllowChat,
            AllowFileAccess = token.AllowFileAccess,
            UsageCount = token.UsageCount,
            LastUsedAt = token.LastUsedAt,
            CreatedAt = token.CreatedAt,
            UpdatedAt = token.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteModuleAccessToken(int id)
    {
        var token = await _context.ModuleAccessTokens.FindAsync(id);
        if (token == null)
        {
            return NotFound(new { message = "Module access token not found" });
        }

        _context.ModuleAccessTokens.Remove(token);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted module access token {Name} with ID {Id}", token.Name, token.Id);

        return Ok(new { message = "Module access token deleted successfully" });
    }
}
