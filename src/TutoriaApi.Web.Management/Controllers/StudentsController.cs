using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Policy = "ProfessorOrAbove")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        IStudentService studentService,
        ILogger<StudentsController> logger)
    {
        _studentService = studentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<StudentDetailDto>>> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? courseId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            var (students, total) = await _studentService.GetPagedAsync(courseId, search, page, size);

            var items = students.Select(u => new StudentDetailDto
            {
                Id = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CourseId = null,
                CourseName = null,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            }).ToList();

            return Ok(new PaginatedResponse<StudentDetailDto>
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
            _logger.LogError(ex, "Error retrieving students");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StudentDetailDto>> GetStudent(int id)
    {
        try
        {
            var student = await _studentService.GetByIdAsync(id);

            if (student == null)
            {
                return NotFound(new { message = "Student not found" });
            }

            return Ok(new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                CourseId = null,
                CourseName = null,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<StudentDetailDto>> CreateStudent([FromBody] StudentCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var student = await _studentService.CreateAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.CourseId);

            _logger.LogInformation("Created student {Username} with ID {Id}", student.Username, student.UserId);

            return CreatedAtAction(nameof(GetStudent), new { id = student.UserId }, new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                CourseId = null,
                CourseName = null,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating student");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StudentDetailDto>> UpdateStudent(int id, [FromBody] StudentUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var student = await _studentService.UpdateAsync(
                id,
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.IsActive,
                request.CourseId);

            _logger.LogInformation("Updated student {Username} with ID {Id}", student.Username, student.UserId);

            return Ok(new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                CourseId = null,
                CourseName = null,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteStudent(int id)
    {
        try
        {
            await _studentService.DeleteAsync(id);

            _logger.LogInformation("Deleted student with ID {Id}", id);

            return Ok(new { message = "Student deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
