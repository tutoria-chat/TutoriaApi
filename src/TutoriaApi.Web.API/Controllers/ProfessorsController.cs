using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

[ApiController]
[Route("api/professors")]
[Authorize(Policy = "ProfessorOrAbove")]
public class ProfessorsController : BaseAuthController // Inherits from BaseAuthController instead of ControllerBase
{
    private readonly IProfessorService _professorService;
    private readonly ILogger<ProfessorsController> _logger;

    public ProfessorsController(
        IProfessorService professorService,
        ILogger<ProfessorsController> _logger)
    {
        _professorService = professorService;
        this._logger = _logger;
    }

    // GetCurrentUserId() and GetCurrentUserFromClaims() are now inherited from BaseAuthController

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProfessorDto>>> GetProfessors(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] bool? isAdmin = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            var currentUser = GetCurrentUserFromClaims();

            var (viewModels, total) = await _professorService.GetPagedAsync(
                universityId,
                courseId,
                isAdmin,
                search,
                page,
                size,
                currentUser);

            var items = viewModels.Select(vm => new ProfessorDto
            {
                Id = vm.Professor.UserId,
                Username = vm.Professor.Username,
                Email = vm.Professor.Email,
                FirstName = vm.Professor.FirstName,
                LastName = vm.Professor.LastName,
                IsAdmin = vm.Professor.IsAdmin ?? false,
                IsActive = vm.Professor.IsActive,
                UniversityId = vm.Professor.UniversityId,
                UniversityName = vm.UniversityName,
                ThemePreference = vm.Professor.ThemePreference ?? "system",
                LanguagePreference = vm.Professor.LanguagePreference ?? "pt-br",
                LastLoginAt = vm.Professor.LastLoginAt,
                CreatedAt = vm.Professor.CreatedAt,
                UpdatedAt = vm.Professor.UpdatedAt,
                CoursesCount = vm.CoursesCount
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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to professors list");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving professors");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfessorDto>> GetCurrentProfessor()
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var viewModel = await _professorService.GetCurrentProfessorAsync(currentUser);

            if (viewModel == null)
            {
                return NotFound(new { message = "Professor not found" });
            }

            var professor = viewModel.Professor;

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = professor.ThemePreference ?? "system",
                LanguagePreference = professor.LanguagePreference ?? "pt-br",
                LastLoginAt = professor.LastLoginAt,
                CreatedAt = professor.CreatedAt,
                UpdatedAt = professor.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to current professor");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current professor");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProfessorDto>> GetProfessor(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();

            var viewModel = await _professorService.GetByIdAsync(id, currentUser);

            if (viewModel == null)
            {
                return NotFound(new { message = "Professor not found" });
            }

            var professor = viewModel.Professor;

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = professor.ThemePreference ?? "system",
                LanguagePreference = professor.LanguagePreference ?? "pt-br",
                LastLoginAt = professor.LastLoginAt,
                CreatedAt = professor.CreatedAt,
                UpdatedAt = professor.UpdatedAt,
                AssignedCourseIds = viewModel.AssignedCourseIds
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to professor {ProfessorId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving professor {ProfessorId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}/courses")]
    public async Task<ActionResult<object>> GetProfessorCourses(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();

            var courseIds = await _professorService.GetProfessorCourseIdsAsync(id, currentUser);

            return Ok(new { course_ids = courseIds });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to professor courses {ProfessorId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving professor courses {ProfessorId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ProfessorDto>> CreateProfessor([FromBody] ProfessorCreateRequest request)
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

            var viewModel = await _professorService.CreateAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                request.UniversityId,
                request.IsAdmin,
                currentUser);

            var professor = viewModel.Professor;

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = professor.ThemePreference ?? "system",
                LanguagePreference = professor.LanguagePreference ?? "pt-br",
                CreatedAt = professor.CreatedAt,
                UpdatedAt = professor.UpdatedAt
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
            _logger.LogWarning(ex, "Unauthorized professor creation attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating professor");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProfessorDto>> UpdateProfessor(int id, [FromBody] ProfessorUpdateRequest request)
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

            var viewModel = await _professorService.UpdateAsync(
                id,
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.IsAdmin,
                request.IsActive,
                currentUser);

            var professor = viewModel.Professor;

            _logger.LogInformation("Updated professor {Username} with ID {Id}", professor.Username, professor.UserId);

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
                UniversityName = viewModel.UniversityName,
                ThemePreference = professor.ThemePreference ?? "system",
                LanguagePreference = professor.LanguagePreference ?? "pt-br",
                LastLoginAt = professor.LastLoginAt,
                CreatedAt = professor.CreatedAt,
                UpdatedAt = professor.UpdatedAt
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
            _logger.LogWarning(ex, "Unauthorized professor update attempt for {ProfessorId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating professor {ProfessorId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> DeleteProfessor(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _professorService.DeleteAsync(id, currentUser);

            _logger.LogInformation("Deleted professor with ID {Id}", id);

            return Ok(new { message = "Professor deleted successfully" });
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
            _logger.LogWarning(ex, "Unauthorized professor deletion attempt for {ProfessorId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting professor {ProfessorId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}/password")]
    public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
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

            await _professorService.ChangePasswordAsync(id, request.NewPassword, currentUser);

            _logger.LogInformation("Changed password for professor with ID {Id}", id);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized password change attempt for {ProfessorId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for professor {ProfessorId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
