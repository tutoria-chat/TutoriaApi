using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Student-facing endpoints — requires only basic authentication (any role including students).
/// Separated from StudentsController which requires ProfessorOrAbove for admin operations.
/// </summary>
[ApiController]
[Route("api/students")]
[Authorize] // Any authenticated user (including students)
public class StudentSelfController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly TutoriaDbContext _dbContext;
    private readonly ILogger<StudentSelfController> _logger;

    public StudentSelfController(
        ICurrentUserService currentUserService,
        TutoriaDbContext dbContext,
        ILogger<StudentSelfController> logger)
    {
        _currentUserService = currentUserService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get the authenticated student's enrolled courses with their modules.
    /// </summary>
    [HttpGet("me/courses")]
    public async Task<ActionResult> GetMyCourses()
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();

            var courseIds = await _dbContext.StudentCourses
                .Where(sc => sc.StudentId == currentUser.UserId)
                .Select(sc => sc.CourseId)
                .ToListAsync();

            var courses = await _dbContext.Courses
                .Where(c => courseIds.Contains(c.Id))
                .Include(c => c.Modules)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Code,
                    c.Description,
                    Modules = c.Modules.Select(m => new
                    {
                        m.Id,
                        m.Name,
                        m.Description,
                    }).ToList()
                })
                .ToListAsync();

            return Ok(courses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving courses for authenticated student");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get an active module access token for a specific module the student is enrolled in.
    /// Returns the first active chat-enabled token for the module.
    /// </summary>
    [HttpGet("me/modules/{moduleId}/token")]
    public async Task<ActionResult> GetModuleToken(int moduleId)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();

            // Verify student is enrolled in a course that contains this module
            var module = await _dbContext.Modules
                .Where(m => m.Id == moduleId)
                .Select(m => new { m.Id, m.CourseId })
                .FirstOrDefaultAsync();

            if (module == null)
                return NotFound(new { message = "Module not found" });

            var isEnrolled = await _dbContext.StudentCourses
                .AnyAsync(sc => sc.StudentId == currentUser.UserId && sc.CourseId == module.CourseId);

            if (!isEnrolled)
                return StatusCode(403, new { message = "You are not enrolled in this module's course" });

            // Get an active, chat-enabled token for this module
            var token = await _dbContext.ModuleAccessTokens
                .Where(t => t.ModuleId == moduleId
                    && t.IsActive
                    && t.AllowChat
                    && (t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Token,
                    t.Name,
                    t.AllowChat,
                    t.AllowFileAccess,
                    t.IsActive,
                    t.ExpiresAt,
                })
                .FirstOrDefaultAsync();

            if (token == null)
                return NotFound(new { message = "No active chat token available for this module" });

            return Ok(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module token for student");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
