using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOrAbove")]
public class UsersController : BaseAuthController
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
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

        try
        {
            var (viewModels, total) = await _userService.GetPagedAsync(
                userType, universityId, isAdmin, isActive, search, page, size);

            var items = viewModels.Select(vm => new UserDto
            {
                UserId = vm.User.UserId,
                Username = vm.User.Username,
                Email = vm.User.Email,
                FirstName = vm.User.FirstName,
                LastName = vm.User.LastName,
                UserType = vm.User.UserType,
                IsActive = vm.User.IsActive,
                IsAdmin = vm.User.IsAdmin,
                UniversityId = vm.User.UniversityId,
                UniversityName = vm.UniversityName,
                ThemePreference = vm.User.ThemePreference ?? "system",
                LanguagePreference = vm.User.LanguagePreference ?? "pt-br",
                LastLoginAt = vm.User.LastLoginAt,
                CreatedAt = vm.User.CreatedAt,
                UpdatedAt = vm.User.UpdatedAt
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        try
        {
            var viewModel = await _userService.GetByIdAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var user = viewModel.User;

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = user.ThemePreference ?? "system",
                LanguagePreference = user.LanguagePreference ?? "pt-br",
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserCreateRequest request)
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

            var viewModel = await _userService.CreateAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                request.UserType,
                request.UniversityId,
                request.CourseId,
                request.IsAdmin,
                request.ThemePreference,
                request.LanguagePreference,
                currentUser);

            var user = viewModel.User;

            _logger.LogInformation("Created user {Username} ({UserType}) with ID {Id}",
                user.Username, user.UserType, user.UserId);

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = user.ThemePreference ?? "system",
                LanguagePreference = user.LanguagePreference ?? "pt-br",
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("only create") || ex.Message.Contains("only update")
                ? StatusCode(403, new { message = ex.Message })
                : BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized user creation attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UserUpdateRequest request)
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

            var viewModel = await _userService.UpdateAsync(
                id,
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.IsAdmin,
                request.IsActive,
                request.UniversityId,
                request.CourseId,
                request.ThemePreference,
                request.LanguagePreference,
                currentUser);

            var user = viewModel.User;

            _logger.LogInformation("Updated user {Username} ({UserType}) with ID {Id}",
                user.Username, user.UserType, user.UserId);

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = user.ThemePreference ?? "system",
                LanguagePreference = user.LanguagePreference ?? "pt-br",
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("only update") || ex.Message.Contains("Cannot update")
                ? BadRequest(new { message = ex.Message })
                : StatusCode(403, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized user update attempt for {UserId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPatch("{id}/activate")]
    public async Task<ActionResult<UserDto>> ActivateUser(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var viewModel = await _userService.ActivateAsync(id, currentUser);
            var user = viewModel.User;

            _logger.LogInformation("Activated user {Username} ({UserType}) with ID {Id}",
                user.Username, user.UserType, user.UserId);

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = user.ThemePreference ?? "system",
                LanguagePreference = user.LanguagePreference ?? "pt-br",
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized user activation attempt for {UserId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<ActionResult<UserDto>> DeactivateUser(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var viewModel = await _userService.DeactivateAsync(id, currentUser);
            var user = viewModel.User;

            _logger.LogInformation("Deactivated user {Username} ({UserType}) with ID {Id}",
                user.Username, user.UserType, user.UserId);

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = user.ThemePreference ?? "system",
                LanguagePreference = user.LanguagePreference ?? "pt-br",
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized user deactivation attempt for {UserId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _userService.DeleteAsync(id, currentUser);

            _logger.LogInformation("Permanently deleted user with ID {Id}", id);

            return Ok(new { message = "User permanently deleted", userId = id, deleted = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}/password")]
    public async Task<ActionResult> ChangeUserPassword(int id, [FromBody] ChangeUserPasswordRequest request)
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

            await _userService.ChangePasswordAsync(id, request.NewPassword, currentUser);

            _logger.LogInformation("Changed password for user with ID {Id}", id);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized password change attempt for {UserId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
