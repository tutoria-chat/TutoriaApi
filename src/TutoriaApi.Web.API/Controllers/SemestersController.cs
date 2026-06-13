using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages institution semesters (term date ranges). When a semester ends,
/// tutoria-api crowns each course's #1 student with the "The One" title.
/// </summary>
[ApiController]
[Route("api/semesters")]
[Authorize(Policy = "AdminOrAbove")]
public class SemestersController : ControllerBase
{
    private readonly ISemesterService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SemestersController> _logger;

    public SemestersController(
        ISemesterService service,
        ICurrentUserService currentUserService,
        ILogger<SemestersController> logger)
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

    private bool CallerOwns(Semester s)
    {
        var caller = GetCallerUniversityId();
        return caller == null || s.UniversityId == caller.Value;
    }

    private static SemesterDto ToDto(Semester s) => new()
    {
        Id = s.Id,
        UniversityId = s.UniversityId,
        UniversityName = s.University?.Name,
        Label = s.Label,
        StartsAtUtc = s.StartsAtUtc,
        EndsAtUtc = s.EndsAtUtc,
        ChampionsAwarded = s.ChampionsAwarded,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    [HttpGet]
    public async Task<ActionResult<List<SemesterDto>>> GetSemesters([FromQuery] int? universityId = null)
    {
        try
        {
            var caller = GetCallerUniversityId();
            if (caller != null && universityId != null && universityId != caller)
                return NotFound(new { message = "University not found" });
            var uniId = caller ?? universityId;
            if (uniId == null)
                return BadRequest(new { message = "universityId is required" });

            var list = await _service.GetByUniversityAsync(uniId.Value);
            return Ok(list.Select(ToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing semesters");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<SemesterDto>> CreateSemester([FromBody] SemesterCreateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var caller = GetCallerUniversityId();
        if (caller != null && request.UniversityId != caller.Value)
            return NotFound(new { message = "University not found" });

        try
        {
            var created = await _service.CreateAsync(new Semester
            {
                UniversityId = caller ?? request.UniversityId,
                Label = request.Label.Trim(),
                StartsAtUtc = DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc),
                EndsAtUtc = DateTime.SpecifyKind(request.EndsAtUtc, DateTimeKind.Utc),
            });
            return CreatedAtAction(nameof(GetSemesters), new { universityId = created.UniversityId }, ToDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating semester");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SemesterDto>> UpdateSemester(int id, [FromBody] SemesterUpdateRequest request)
    {
        try
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null || !CallerOwns(existing))
                return NotFound(new { message = "Semester not found" });

            var updated = await _service.UpdateAsync(id, request.Label, request.StartsAtUtc, request.EndsAtUtc);
            return Ok(ToDto(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Semester not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating semester {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteSemester(int id)
    {
        try
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null || !CallerOwns(existing))
                return NotFound(new { message = "Semester not found" });

            await _service.DeleteAsync(id);
            return Ok(new { message = "Semester deleted" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Semester not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting semester {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
