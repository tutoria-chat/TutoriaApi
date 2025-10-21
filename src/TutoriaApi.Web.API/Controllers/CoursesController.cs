using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages academic courses within universities.
/// </summary>
/// <remarks>
/// Courses are programs of study offered by universities. Each course belongs to one university
/// and can have multiple modules, students, and professors assigned to it.
///
/// **Authorization**: All endpoints require authentication. Write operations require AdminOrAbove policy.
///
/// **Related Entities**:
/// - University (parent)
/// - Modules (children)
/// - Students (many-to-one)
/// - Professors (many-to-many via ProfessorCourses)
/// </remarks>
[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseService courseService,
        ILogger<CoursesController> logger)
    {
        _courseService = courseService;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of courses with filtering.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 10, max: 100)</param>
    /// <param name="universityId">Filter by university ID (optional)</param>
    /// <param name="professorId">Filter by professor ID - only show courses assigned to this professor (optional)</param>
    /// <param name="search">Search by course name or code (optional)</param>
    /// <returns>Paginated list of courses with university info and entity counts.</returns>
    /// <remarks>
    /// Returns a paginated list of courses with related entity counts (modules, professors, students).
    ///
    /// **Filtering**:
    /// - universityId: Return only courses from specified university
    /// - professorId: Return only courses assigned to this professor
    /// - search: Partial match on course name or code
    ///
    /// **Performance**: Uses single query with projections to avoid N+1 queries.
    /// </remarks>
    /// <response code="200">Returns paginated course list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<CourseDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<CourseDetailDto>>> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? universityId = null,
        [FromQuery] int? professorId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            var (viewModels, total) = await _courseService.GetPagedWithCountsAsync(universityId, professorId, search, page, size);

            var dtos = viewModels.Select(vm => new CourseDetailDto
            {
                Id = vm.Course.Id,
                Name = vm.Course.Name,
                Code = vm.Course.Code,
                Description = vm.Course.Description,
                UniversityId = vm.Course.UniversityId,
                UniversityName = vm.UniversityName,
                ModulesCount = vm.ModulesCount,
                ProfessorsCount = vm.ProfessorsCount,
                StudentsCount = vm.StudentsCount,
                CreatedAt = vm.Course.CreatedAt,
                UpdatedAt = vm.Course.UpdatedAt
            }).ToList();

            return Ok(new PaginatedResponse<CourseDetailDto>
            {
                Items = dtos,
                Total = total,
                Page = page,
                Size = size,
                Pages = (int)Math.Ceiling(total / (double)size)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paginated courses");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CourseWithDetailsDto>> GetCourse(int id)
    {
        try
        {
            var viewModel = await _courseService.GetCourseWithFullDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            var dto = new CourseWithDetailsDto
            {
                Id = viewModel.Course.Id,
                Name = viewModel.Course.Name,
                Code = viewModel.Course.Code,
                Description = viewModel.Course.Description,
                UniversityId = viewModel.Course.UniversityId,
                University = viewModel.University != null ? new UniversityDto
                {
                    Id = viewModel.University.Id,
                    Name = viewModel.University.Name,
                    Code = viewModel.University.Code,
                    Description = viewModel.University.Description,
                    CreatedAt = viewModel.University.CreatedAt,
                    UpdatedAt = viewModel.University.UpdatedAt
                } : null,
                Modules = viewModel.Modules.Select(m => new ModuleDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Code = m.Code,
                    Description = m.Description,
                    Semester = m.Semester,
                    Year = m.Year,
                    FilesCount = viewModel.ModuleFileCounts.GetValueOrDefault(m.Id, 0),
                    TokensCount = viewModel.ModuleTokenCounts.GetValueOrDefault(m.Id, 0),
                    UpdatedAt = m.UpdatedAt
                }).ToList(),
                Students = viewModel.Students.Select(s => new StudentDto
                {
                    Id = s.UserId,
                    Username = s.Username,
                    Email = s.Email,
                    FirstName = s.FirstName,
                    LastName = s.LastName
                }).ToList(),
                CreatedAt = viewModel.Course.CreatedAt,
                UpdatedAt = viewModel.Course.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting course with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<CourseDetailDto>> CreateCourse([FromBody] CourseCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var course = new Course
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                UniversityId = request.UniversityId
            };

            var created = await _courseService.CreateAsync(course);

            _logger.LogInformation("Created course {Name} with ID {Id}", created.Name, created.Id);

            // Get full details for response
            var viewModel = await _courseService.GetCourseWithCountsAsync(created.Id);

            var dto = new CourseDetailDto
            {
                Id = viewModel!.Course.Id,
                Name = viewModel.Course.Name,
                Code = viewModel.Course.Code,
                Description = viewModel.Course.Description,
                UniversityId = viewModel.Course.UniversityId,
                UniversityName = viewModel.UniversityName,
                ModulesCount = viewModel.ModulesCount,
                ProfessorsCount = viewModel.ProfessorsCount,
                StudentsCount = viewModel.StudentsCount,
                CreatedAt = viewModel.Course.CreatedAt,
                UpdatedAt = viewModel.Course.UpdatedAt
            };

            return CreatedAtAction(nameof(GetCourse), new { id = created.Id }, dto);
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
            _logger.LogError(ex, "Error creating course");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<CourseDetailDto>> UpdateCourse(int id, [FromBody] CourseUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var course = new Course
            {
                Name = request.Name ?? string.Empty,
                Code = request.Code ?? string.Empty,
                Description = request.Description
            };

            var viewModel = await _courseService.UpdateAsync(id, course);

            _logger.LogInformation("Updated course {Name} with ID {Id}", viewModel.Course.Name, viewModel.Course.Id);

            var dto = new CourseDetailDto
            {
                Id = viewModel.Course.Id,
                Name = viewModel.Course.Name,
                Code = viewModel.Course.Code,
                Description = viewModel.Course.Description,
                UniversityId = viewModel.Course.UniversityId,
                UniversityName = viewModel.UniversityName,
                ModulesCount = viewModel.ModulesCount,
                ProfessorsCount = viewModel.ProfessorsCount,
                StudentsCount = viewModel.StudentsCount,
                CreatedAt = viewModel.Course.CreatedAt,
                UpdatedAt = viewModel.Course.UpdatedAt
            };

            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Course not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> DeleteCourse(int id)
    {
        try
        {
            await _courseService.DeleteAsync(id);

            _logger.LogInformation("Deleted course with ID {Id}", id);

            return Ok(new { message = "Course deleted successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Course not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost("{courseId}/professors/{professorId}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> AssignProfessorToCourse(int courseId, int professorId)
    {
        try
        {
            await _courseService.AssignProfessorAsync(courseId, professorId);

            _logger.LogInformation("Assigned professor {ProfessorId} to course {CourseId}", professorId, courseId);

            return Ok(new { message = "Professor assigned to course successfully" });
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
            _logger.LogError(ex, "Error assigning professor {ProfessorId} to course {CourseId}", professorId, courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{courseId}/professors/{professorId}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> UnassignProfessorFromCourse(int courseId, int professorId)
    {
        try
        {
            await _courseService.UnassignProfessorAsync(courseId, professorId);

            _logger.LogInformation("Unassigned professor {ProfessorId} from course {CourseId}", professorId, courseId);

            return Ok(new { message = "Professor unassigned from course successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning professor {ProfessorId} from course {CourseId}", professorId, courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
