using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Policy = "ProfessorOrAbove")] // Require ProfessorOrAbove for all student operations
public class StudentsController : ControllerBase
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        TutoriaDbContext context,
        ILogger<StudentsController> logger)
    {
        _context = context;
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

        var query = _context.Users
            .Where(u => u.UserType == "student")
            .Include(u => u.Course)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(u => u.CourseId == courseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var students = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = students.Select(u => new StudentDetailDto
        {
            Id = u.UserId,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            IsActive = u.IsActive,
            CourseId = u.CourseId ?? 0,
            CourseName = u.Course?.Name,
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

    [HttpGet("{id}")]
    public async Task<ActionResult<StudentDetailDto>> GetStudent(int id)
    {
        var student = await _context.Users
            .Where(u => u.UserType == "student")
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.UserId == id);

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
            CourseId = student.CourseId ?? 0,
            CourseName = student.Course?.Name,
            LastLoginAt = student.LastLoginAt,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<StudentDetailDto>> CreateStudent([FromBody] StudentCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if course exists
        var course = await _context.Courses.FindAsync(request.CourseId);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        // Check if username or email already exists (across all users)
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingByUsername != null)
        {
            return BadRequest(new { message = "Username already exists" });
        }

        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingByEmail != null)
        {
            return BadRequest(new { message = "Email already exists" });
        }

        var student = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            HashedPassword = null, // Students don't have passwords - they don't login
            UserType = "student",
            CourseId = request.CourseId,
            IsActive = true
        };

        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created student {Username} with ID {Id}", student.Username, student.UserId);

        return CreatedAtAction(nameof(GetStudent), new { id = student.UserId }, new StudentDetailDto
        {
            Id = student.UserId,
            Username = student.Username,
            Email = student.Email,
            FirstName = student.FirstName,
            LastName = student.LastName,
            IsActive = student.IsActive,
            CourseId = student.CourseId ?? 0,
            CourseName = course.Name,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StudentDetailDto>> UpdateStudent(int id, [FromBody] StudentUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var student = await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (student == null)
        {
            return NotFound(new { message = "Student not found" });
        }

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.UserId != id);

            if (existingByUsername != null)
            {
                return BadRequest(new { message = "Username already exists" });
            }

            student.Username = request.Username;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != id);

            if (existingByEmail != null)
            {
                return BadRequest(new { message = "Email already exists" });
            }

            student.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            student.FirstName = request.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            student.LastName = request.LastName;
        }

        if (request.IsActive.HasValue)
        {
            student.IsActive = request.IsActive.Value;
        }

        if (request.CourseId.HasValue)
        {
            var course = await _context.Courses.FindAsync(request.CourseId.Value);
            if (course == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            student.CourseId = request.CourseId.Value;
        }

        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated student {Username} with ID {Id}", student.Username, student.UserId);

        var updatedCourse = await _context.Courses.FindAsync(student.CourseId);

        return Ok(new StudentDetailDto
        {
            Id = student.UserId,
            Username = student.Username,
            Email = student.Email,
            FirstName = student.FirstName,
            LastName = student.LastName,
            IsActive = student.IsActive,
            CourseId = student.CourseId ?? 0,
            CourseName = updatedCourse?.Name,
            LastLoginAt = student.LastLoginAt,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteStudent(int id)
    {
        var student = await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (student == null)
        {
            return NotFound(new { message = "Student not found" });
        }

        _context.Users.Remove(student);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted student {Username} with ID {Id}", student.Username, student.UserId);

        return Ok(new { message = "Student deleted successfully" });
    }

    // Students don't have passwords - they don't login
    // Password management is not available for student user type
}
