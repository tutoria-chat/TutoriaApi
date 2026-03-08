using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages AI provider API keys (OpenAI, Anthropic, etc.).
/// </summary>
/// <remarks>
/// Provider keys are encrypted API keys used to authenticate with AI providers.
/// This controller allows SuperAdmins to manage key rotation, priorities, and cooldowns.
///
/// **Authorization**: SuperAdmin only for all operations.
/// </remarks>
[ApiController]
[Route("api/provider-keys")]
[Authorize(Policy = "SuperAdminOnly")]
public class ProviderKeysController : ControllerBase
{
    private readonly IProviderKeyService _providerKeyService;
    private readonly ILogger<ProviderKeysController> _logger;

    public ProviderKeysController(
        IProviderKeyService providerKeyService,
        ILogger<ProviderKeysController> logger)
    {
        _providerKeyService = providerKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Get all provider keys (with masked API keys).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProviderKeyListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProviderKeyListDto>>> GetProviderKeys()
    {
        try
        {
            var keys = await _providerKeyService.GetAllAsync();

            var dtos = new List<ProviderKeyListDto>();
            foreach (var key in keys)
            {
                var maskedKey = await _providerKeyService.MaskApiKey(key.EncryptedApiKey);
                dtos.Add(new ProviderKeyListDto
                {
                    Id = key.Id,
                    Provider = key.Provider,
                    KeyName = key.KeyName,
                    MaskedKey = maskedKey,
                    Region = key.Region,
                    Priority = key.Priority,
                    IsActive = key.IsActive,
                    LastUsedAt = key.LastUsedAt,
                    FailureCount = key.FailureCount,
                    LastFailureAt = key.LastFailureAt,
                    CooldownUntil = key.CooldownUntil,
                    CreatedAt = key.CreatedAt,
                    UpdatedAt = key.UpdatedAt
                });
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting provider keys");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get a specific provider key by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProviderKeyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderKeyDetailDto>> GetProviderKey(int id)
    {
        try
        {
            var key = await _providerKeyService.GetByIdAsync(id);
            if (key == null)
            {
                return NotFound(new { message = "Provider key not found" });
            }

            var maskedKey = await _providerKeyService.MaskApiKey(key.EncryptedApiKey);

            var dto = new ProviderKeyDetailDto
            {
                Id = key.Id,
                Provider = key.Provider,
                KeyName = key.KeyName,
                MaskedKey = maskedKey,
                Region = key.Region,
                Priority = key.Priority,
                IsActive = key.IsActive,
                LastUsedAt = key.LastUsedAt,
                FailureCount = key.FailureCount,
                LastFailureAt = key.LastFailureAt,
                CooldownUntil = key.CooldownUntil,
                CreatedAt = key.CreatedAt,
                UpdatedAt = key.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting provider key with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new provider key (API key is encrypted before storage).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProviderKeyDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProviderKeyDetailDto>> CreateProviderKey([FromBody] ProviderKeyCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var providerKey = new ProviderKey
            {
                Provider = request.Provider,
                KeyName = request.KeyName,
                EncryptedApiKey = string.Empty, // Will be set by service
                Region = request.Region,
                Priority = request.Priority,
                IsActive = request.IsActive
            };

            var created = await _providerKeyService.CreateAsync(providerKey, request.ApiKey);

            _logger.LogInformation("Created provider key '{KeyName}' for provider '{Provider}' with ID {Id}",
                created.KeyName, created.Provider, created.Id);

            var maskedKey = await _providerKeyService.MaskApiKey(created.EncryptedApiKey);

            var dto = new ProviderKeyDetailDto
            {
                Id = created.Id,
                Provider = created.Provider,
                KeyName = created.KeyName,
                MaskedKey = maskedKey,
                Region = created.Region,
                Priority = created.Priority,
                IsActive = created.IsActive,
                LastUsedAt = created.LastUsedAt,
                FailureCount = created.FailureCount,
                LastFailureAt = created.LastFailureAt,
                CooldownUntil = created.CooldownUntil,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetProviderKey), new { id = created.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating provider key");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update an existing provider key.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProviderKeyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderKeyDetailDto>> UpdateProviderKey(int id, [FromBody] ProviderKeyUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var providerKey = new ProviderKey
            {
                Provider = request.Provider,
                KeyName = request.KeyName,
                EncryptedApiKey = string.Empty, // Will be handled by service
                Region = request.Region,
                Priority = request.Priority,
                IsActive = request.IsActive
            };

            var updated = await _providerKeyService.UpdateAsync(id, providerKey, request.ApiKey);

            _logger.LogInformation("Updated provider key '{KeyName}' with ID {Id}", updated.KeyName, updated.Id);

            var maskedKey = await _providerKeyService.MaskApiKey(updated.EncryptedApiKey);

            var dto = new ProviderKeyDetailDto
            {
                Id = updated.Id,
                Provider = updated.Provider,
                KeyName = updated.KeyName,
                MaskedKey = maskedKey,
                Region = updated.Region,
                Priority = updated.Priority,
                IsActive = updated.IsActive,
                LastUsedAt = updated.LastUsedAt,
                FailureCount = updated.FailureCount,
                LastFailureAt = updated.LastFailureAt,
                CooldownUntil = updated.CooldownUntil,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };

            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Provider key not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating provider key with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a provider key.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteProviderKey(int id)
    {
        try
        {
            await _providerKeyService.DeleteAsync(id);

            _logger.LogInformation("Deleted provider key with ID {Id}", id);

            return Ok(new { message = "Provider key deleted successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Provider key not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting provider key with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
