using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages a university's Majors (graduações / degree programs). Courses are
/// tagged with the Majors they belong to; the institution starts from a
/// standard list and may add its own.
/// </summary>
[ApiController]
[Route("api/universities/{universityId:int}/majors")]
[Authorize(Policy = "AdminOrAbove")]
public class MajorsController : ControllerBase
{
    private readonly IMajorService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MajorsController> _logger;

    public MajorsController(
        IMajorService service,
        ICurrentUserService currentUserService,
        ILogger<MajorsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>Caller's effective university (null = super admin, not scoped).</summary>
    private int? GetCallerUniversityId()
    {
        var user = _currentUserService.GetCurrentUser();
        return user.UserType == "super_admin" ? null : user.UniversityId;
    }

    /// <summary>True when the caller may manage majors for the given university.</summary>
    private bool CallerOwns(int universityId)
    {
        var caller = GetCallerUniversityId();
        return caller == null || caller.Value == universityId;
    }

    private static MajorDto ToDto(Major m) => new()
    {
        Id = m.Id,
        UniversityId = m.UniversityId,
        Name = m.Name,
        CreatedAt = m.CreatedAt,
    };

    [HttpGet]
    public async Task<ActionResult<List<MajorDto>>> GetMajors(int universityId)
    {
        if (!CallerOwns(universityId))
            return NotFound(new { message = "University not found" });

        try
        {
            var list = await _service.GetByUniversityAsync(universityId);
            return Ok(list.Select(ToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing majors for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<MajorDto>> CreateMajor(int universityId, [FromBody] MajorCreateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!CallerOwns(universityId))
            return NotFound(new { message = "University not found" });

        try
        {
            var created = await _service.CreateAsync(universityId, request.Name);
            return Ok(ToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating major for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{majorId:int}")]
    public async Task<ActionResult> DeleteMajor(int universityId, int majorId)
    {
        if (!CallerOwns(universityId))
            return NotFound(new { message = "University not found" });

        try
        {
            await _service.DeleteAsync(universityId, majorId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting major {MajorId}", majorId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Add the standard majors list (any not already present) and return the full list.</summary>
    [HttpPost("seed-defaults")]
    public async Task<ActionResult<List<MajorDto>>> SeedDefaults(int universityId)
    {
        if (!CallerOwns(universityId))
            return NotFound(new { message = "University not found" });

        try
        {
            var list = await _service.SeedDefaultsAsync(universityId);
            return Ok(list.Select(ToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default majors for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
