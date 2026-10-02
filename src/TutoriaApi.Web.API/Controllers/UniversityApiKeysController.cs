using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// An institution's keys for the external API — what admins paste into the
/// Moodle grading assistant plugin (quiz_tutoria). Super admins manage any
/// institution; managers only their own (enforced in the service).
/// </summary>
[ApiController]
[Route("api/universities/{universityId:int}/api-keys")]
[Authorize(Policy = "AdminOrAbove")]
public class UniversityApiKeysController : ControllerBase
{
    private readonly IUniversityApiKeyService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UniversityApiKeysController> _logger;

    public UniversityApiKeysController(
        IUniversityApiKeyService service,
        ICurrentUserService currentUserService,
        ILogger<UniversityApiKeysController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>The institution id/name and whether grading is enabled for it.</summary>
    [HttpGet("setup-info")]
    public async Task<ActionResult<UniversityApiKeySetupInfoDto>> GetSetupInfo(int universityId)
    {
        try
        {
            var info = await _service.GetSetupInfoAsync(universityId, _currentUserService.GetCurrentUser());
            return Ok(new UniversityApiKeySetupInfoDto
            {
                UniversityId = info.UniversityId,
                UniversityName = info.UniversityName,
                Enabled = info.Enabled,
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API key setup info for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UniversityApiKeyDto>>> GetAll(int universityId)
    {
        try
        {
            var keys = await _service.ListAsync(universityId, _currentUserService.GetCurrentUser());
            return Ok(keys.Select(ToDto));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing API keys for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Creates a key. The response carries the full key — the only time it is shown.</summary>
    [HttpPost]
    public async Task<ActionResult<CreatedUniversityApiKeyDto>> Create(
        int universityId, [FromBody] CreateUniversityApiKeyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _service.CreateAsync(universityId, request.Name, _currentUserService.GetCurrentUser());
            var dto = new CreatedUniversityApiKeyDto { Key = created.PlainTextKey };
            Fill(dto, created.Key);
            return StatusCode(201, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key for university {UniversityId}", universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Revokes a key — it stops working immediately.</summary>
    [HttpDelete("{keyId:int}")]
    public async Task<IActionResult> Revoke(int universityId, int keyId)
    {
        try
        {
            await _service.RevokeAsync(universityId, keyId, _currentUserService.GetCurrentUser());
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key {KeyId} for university {UniversityId}", keyId, universityId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static UniversityApiKeyDto ToDto(UniversityApiKey key)
    {
        var dto = new UniversityApiKeyDto();
        Fill(dto, key);
        return dto;
    }

    private static void Fill(UniversityApiKeyDto dto, UniversityApiKey key)
    {
        dto.Id = key.Id;
        dto.Name = key.Name;
        dto.KeyPrefix = key.KeyPrefix;
        dto.CreatedAt = key.CreatedAt;
        dto.LastUsedAt = key.LastUsedAt;
        dto.RevokedAt = key.RevokedAt;
        dto.IsActive = key.RevokedAt == null;
    }
}
