using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages LTI 1.3 platform registrations from the dashboard.
/// </summary>
/// <remarks>
/// Connecting an LMS is a two-way exchange:
/// 1. GET /api/lti/registrations/setup-info returns the three URLs the LMS admin
///    pastes into their platform.
/// 2. The LMS then issues a Platform ID, Client ID and Deployment ID, which come
///    back here via POST to complete the trust relationship.
///
/// **Authorization**: AdminOrAbove. Super admins manage every institution;
/// everyone else is limited to their own.
/// </remarks>
[ApiController]
[Route("api/lti/registrations")]
[Authorize(Policy = "AdminOrAbove")]
public class LtiRegistrationsController : ControllerBase
{
    private readonly ILtiRegistrationService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LtiRegistrationsController> _logger;

    public LtiRegistrationsController(
        ILtiRegistrationService service,
        ICurrentUserService currentUserService,
        ILogger<LtiRegistrationsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>The URLs to paste into the LMS when registering Tutoria.</summary>
    [HttpGet("setup-info")]
    public ActionResult<LtiSetupInfoDto> GetSetupInfo()
    {
        try
        {
            var info = _service.GetSetupInfo(GetRequestOrigin());

            return Ok(new LtiSetupInfoDto
            {
                LoginUrl = info.LoginUrl,
                LaunchUrl = info.LaunchUrl,
                JwksUrl = info.JwksUrl,
                Enabled = info.Enabled,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building LTI setup info");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LtiRegistrationDto>>> GetAll()
    {
        try
        {
            var registrations = await _service.GetAllAsync(_currentUserService.GetCurrentUser());
            return Ok(registrations.Select(ToDto));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing LTI registrations");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LtiRegistrationDto>> GetById(int id)
    {
        try
        {
            var registration = await _service.GetByIdAsync(id, _currentUserService.GetCurrentUser());
            return registration == null
                ? NotFound(new { message = "Registro LTI não encontrado." })
                : Ok(ToDto(registration));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting LTI registration {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Registers a platform together with its first deployment.</summary>
    [HttpPost]
    public async Task<ActionResult<LtiRegistrationDto>> Create([FromBody] LtiRegistrationCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _service.CreateAsync(ToInput(request), _currentUserService.GetCurrentUser());
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDto(created));
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating LTI registration");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<LtiRegistrationDto>> Update(int id, [FromBody] LtiRegistrationUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _service.UpdateAsync(id, new LtiRegistrationInput
            {
                Name = request.Name,
                AuthLoginUrl = request.AuthLoginUrl,
                AuthTokenUrl = request.AuthTokenUrl,
                KeySetUrl = request.KeySetUrl,
                IsActive = request.IsActive,
            }, _currentUserService.GetCurrentUser());

            return Ok(ToDto(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating LTI registration {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id, _currentUserService.GetCurrentUser());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting LTI registration {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Adds another deployment to an existing platform registration.</summary>
    [HttpPost("{id}/deployments")]
    public async Task<ActionResult<LtiDeploymentDto>> AddDeployment(int id, [FromBody] LtiDeploymentDto request)
    {
        try
        {
            var deployment = await _service.AddDeploymentAsync(
                id, request.DeploymentId, _currentUserService.GetCurrentUser());

            return Ok(new LtiDeploymentDto
            {
                Id = deployment.Id,
                DeploymentId = deployment.DeploymentId,
                IsActive = deployment.IsActive,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding deployment to LTI registration {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>LMS courses seen on launches, and which Tutoria course each maps to.</summary>
    [HttpGet("{id}/contexts")]
    public async Task<ActionResult<IEnumerable<LtiContextMappingDto>>> GetContexts(int id)
    {
        try
        {
            var mappings = await _service.GetContextMappingsAsync(id, _currentUserService.GetCurrentUser());
            return Ok(mappings.Select(ToDto));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing LTI contexts for registration {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Links an LMS course to a Tutoria course (null unlinks it).</summary>
    [HttpPut("{id}/contexts/{mappingId}")]
    public async Task<ActionResult<LtiContextMappingDto>> SetContextCourse(
        int id, int mappingId, [FromBody] LtiContextMappingUpdateRequest request)
    {
        try
        {
            var mapping = await _service.SetContextCourseAsync(
                id, mappingId, request.CourseId, _currentUserService.GetCurrentUser());

            return Ok(ToDto(mapping));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping LTI context {MappingId}", mappingId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    // -----------------------------------------------------------------

    /// <summary>
    /// The origin this request arrived on, used when no LTI base URL is configured.
    /// </summary>
    private string GetRequestOrigin() => $"{Request.Scheme}://{Request.Host}";

    private static LtiRegistrationInput ToInput(LtiRegistrationCreateRequest r) => new()
    {
        Issuer = r.Issuer,
        ClientId = r.ClientId,
        DeploymentId = r.DeploymentId,
        AuthLoginUrl = r.AuthLoginUrl,
        AuthTokenUrl = r.AuthTokenUrl,
        KeySetUrl = r.KeySetUrl,
        Name = r.Name,
        UniversityId = r.UniversityId,
    };

    private static LtiRegistrationDto ToDto(LtiRegistration r) => new()
    {
        Id = r.Id,
        Issuer = r.Issuer,
        ClientId = r.ClientId,
        AuthLoginUrl = r.AuthLoginUrl,
        AuthTokenUrl = r.AuthTokenUrl,
        KeySetUrl = r.KeySetUrl,
        Name = r.Name,
        UniversityId = r.UniversityId,
        UniversityName = r.University?.Name,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt,
        Deployments = r.Deployments.Select(d => new LtiDeploymentDto
        {
            Id = d.Id,
            DeploymentId = d.DeploymentId,
            IsActive = d.IsActive,
        }).ToList(),
    };

    private static LtiContextMappingDto ToDto(LtiContextMapping m) => new()
    {
        Id = m.Id,
        ContextId = m.ContextId,
        ContextTitle = m.ContextTitle,
        ContextLabel = m.ContextLabel,
        CourseId = m.CourseId,
        CourseName = m.Course?.Name,
        LastSeenAt = m.LastSeenAt,
    };
}
