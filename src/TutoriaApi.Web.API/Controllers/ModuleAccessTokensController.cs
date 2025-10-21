using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

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
[Route("api/[controller]")]
[Authorize(Policy = "ProfessorOrAbove")]
public class ModuleAccessTokensController : BaseAuthController
{
    private readonly IModuleAccessTokenService _tokenService;
    private readonly ILogger<ModuleAccessTokensController> _logger;

    public ModuleAccessTokensController(
        IModuleAccessTokenService tokenService,
        ILogger<ModuleAccessTokensController> logger)
    {
        _tokenService = tokenService;
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

        try
        {
            var currentUser = GetCurrentUserFromClaims();

            var (viewModels, total) = await _tokenService.GetPagedAsync(
                moduleId,
                universityId,
                isActive,
                page,
                size,
                currentUser);

            var items = viewModels.Select(vm => new ModuleAccessTokenListDto
            {
                Id = vm.Token.Id,
                Token = vm.Token.Token,
                Name = vm.Token.Name,
                Description = vm.Token.Description,
                ModuleId = vm.Token.ModuleId,
                ModuleName = vm.ModuleName,
                IsActive = vm.Token.IsActive,
                ExpiresAt = vm.Token.ExpiresAt,
                AllowChat = vm.Token.AllowChat,
                AllowFileAccess = vm.Token.AllowFileAccess,
                UsageCount = vm.Token.UsageCount,
                LastUsedAt = vm.Token.LastUsedAt,
                CreatedAt = vm.Token.CreatedAt
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module access tokens");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModuleAccessTokenDetailDto>> GetModuleAccessToken(int id)
    {
        try
        {
            var viewModel = await _tokenService.GetWithDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "Module access token not found" });
            }

            var token = viewModel.Token;

            return Ok(new ModuleAccessTokenDetailDto
            {
                Id = token.Id,
                Token = token.Token,
                Name = token.Name,
                Description = token.Description,
                ModuleId = token.ModuleId,
                ModuleName = viewModel.ModuleName,
                CourseId = token.Module?.CourseId,
                CourseName = viewModel.CourseName,
                UniversityId = token.Module?.Course?.UniversityId,
                UniversityName = viewModel.UniversityName,
                CreatedByProfessorId = token.CreatedByProfessorId,
                CreatedByName = viewModel.CreatedByName,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module access token {TokenId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ModuleAccessTokenDetailDto>> CreateModuleAccessToken(
        [FromBody] ModuleAccessTokenCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var viewModel = await _tokenService.CreateAsync(
                request.ModuleId,
                request.Name,
                request.Description,
                request.AllowChat,
                request.AllowFileAccess,
                request.ExpiresInDays,
                currentUser);

            var token = viewModel.Token;

            _logger.LogInformation("Created module access token {Name} for module {ModuleId}",
                token.Name, token.ModuleId);

            return CreatedAtAction(nameof(GetModuleAccessToken), new { id = token.Id }, new ModuleAccessTokenDetailDto
            {
                Id = token.Id,
                Token = token.Token,
                Name = token.Name,
                Description = token.Description,
                ModuleId = token.ModuleId,
                ModuleName = viewModel.ModuleName,
                CourseId = token.Module?.CourseId,
                CourseName = viewModel.CourseName,
                UniversityId = token.Module?.Course?.UniversityId,
                UniversityName = viewModel.UniversityName,
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating module access token");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
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

        try
        {
            var viewModel = await _tokenService.UpdateAsync(
                id,
                request.Name,
                request.Description,
                request.IsActive,
                request.AllowChat,
                request.AllowFileAccess);

            var token = viewModel.Token;

            _logger.LogInformation("Updated module access token {Name} with ID {Id}", token.Name, token.Id);

            return Ok(new ModuleAccessTokenDetailDto
            {
                Id = token.Id,
                Token = token.Token,
                Name = token.Name,
                Description = token.Description,
                ModuleId = token.ModuleId,
                ModuleName = viewModel.ModuleName,
                CourseId = token.Module?.CourseId,
                CourseName = viewModel.CourseName,
                UniversityId = token.Module?.Course?.UniversityId,
                UniversityName = viewModel.UniversityName,
                CreatedByProfessorId = token.CreatedByProfessorId,
                CreatedByName = viewModel.CreatedByName,
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module access token {TokenId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteModuleAccessToken(int id)
    {
        try
        {
            await _tokenService.DeleteAsync(id);

            _logger.LogInformation("Deleted module access token with ID {Id}", id);

            return Ok(new { message = "Module access token deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting module access token {TokenId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
