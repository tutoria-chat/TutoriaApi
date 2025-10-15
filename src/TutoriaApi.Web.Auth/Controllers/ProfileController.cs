using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Auth.DTOs;
using BCrypt.Net;

namespace TutoriaApi.Web.Auth.Controllers;

[ApiController]
[Route("api/auth/me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        TutoriaDbContext context,
        ILogger<ProfileController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();

            var user = await _context.Users
                .Include(u => u.University)
                .Include(u => u.Course)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                IsActive = user.IsActive,
                UniversityId = user.UniversityId,
                UniversityName = user.University?.Name,
                IsAdmin = user.IsAdmin,
                CourseId = user.CourseId,
                CourseName = user.Course?.Name,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                ThemePreference = user.ThemePreference,
                LanguagePreference = user.LanguagePreference
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid authentication token" });
        }
    }

    [HttpPut]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetCurrentUserId();

            var user = await _context.Users
                .Include(u => u.University)
                .Include(u => u.Course)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Update fields if provided
            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                // Check if email is already taken by another user
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != userId);

                if (existingUser != null)
                {
                    return BadRequest(new { message = "Email already in use" });
                }

                user.Email = request.Email;
            }

            if (!string.IsNullOrWhiteSpace(request.ThemePreference))
            {
                user.ThemePreference = request.ThemePreference;
            }

            if (!string.IsNullOrWhiteSpace(request.LanguagePreference))
            {
                user.LanguagePreference = request.LanguagePreference;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated their profile", userId);

            return Ok(new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                IsActive = user.IsActive,
                UniversityId = user.UniversityId,
                UniversityName = user.University?.Name,
                IsAdmin = user.IsAdmin,
                CourseId = user.CourseId,
                CourseName = user.Course?.Name,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                ThemePreference = user.ThemePreference,
                LanguagePreference = user.LanguagePreference
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid authentication token" });
        }
    }

    [HttpPut("password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetCurrentUserId();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.HashedPassword))
            {
                _logger.LogWarning("Password change failed: Invalid current password for user {UserId}", userId);
                return BadRequest(new { message = "Current password is incorrect" });
            }

            // Update password
            user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} changed their password", userId);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid authentication token" });
        }
    }
}
