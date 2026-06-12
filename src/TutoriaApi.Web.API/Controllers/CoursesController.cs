using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;
using TutoriaApi.Web.API.Helpers;

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
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseRepository _courseRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IGamificationStatsRepository _gamificationStatsRepository;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseService courseService,
        ICurrentUserService currentUserService,
        ICourseRepository courseRepository,
        ISubscriptionRepository subscriptionRepository,
        IUniversityRepository universityRepository,
        IGamificationStatsRepository gamificationStatsRepository,
        ILogger<CoursesController> logger)
    {
        _courseService = courseService;
        _currentUserService = currentUserService;
        _courseRepository = courseRepository;
        _subscriptionRepository = subscriptionRepository;
        _universityRepository = universityRepository;
        _gamificationStatsRepository = gamificationStatsRepository;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────────
    //  Tenant isolation helpers
    // ────────────────────────────────────────────────────────────────

    private int? GetCallerUniversityId()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.UserType == "super_admin")
            return null;
        return currentUser.UniversityId;
    }

    private async Task<bool> CallerOwnsCourseAsync(int courseId)
    {
        var callerUniversityId = GetCallerUniversityId();
        if (callerUniversityId == null) return true;

        var course = await _courseRepository.GetByIdAsync(courseId);
        return course != null && course.UniversityId == callerUniversityId.Value;
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

        // Tenant isolation: non-super-admins are always scoped to their own university.
        // If a non-super-admin has no university in their token, return empty (fail-safe).
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.UserType != "super_admin")
        {
            if (!currentUser.UniversityId.HasValue)
            {
                return Ok(new PaginatedResponse<CourseDetailDto>
                {
                    Items = new List<CourseDetailDto>(),
                    Total = 0, Page = page, Size = size, Pages = 0
                });
            }
            universityId = currentUser.UniversityId.Value;
        }

        try
        {
            var (viewModels, total) = await _courseService.GetPagedWithCountsAsync(universityId, professorId, search, page, size, currentUser);

            var dtos = viewModels.Select(vm => new CourseDetailDto
            {
                Id = vm.Course.Id,
                Name = vm.Course.Name,
                Code = vm.Course.Code,
                Description = vm.Course.Description,
                UniversityId = vm.Course.UniversityId,
                UniversityName = vm.UniversityName,
                ExternalCourseId = vm.Course.ExternalCourseId,
                TitleTracks = vm.Course.TitleTracks,
                EnableEnem = vm.Course.EnableEnem,
                EnemArea = vm.Course.EnemArea,
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
            // Tenant isolation: verify caller has access to this course
            if (!await CallerOwnsCourseAsync(id))
            {
                return NotFound(new { message = "Course not found" });
            }

            var viewModel = await _courseService.GetCourseWithFullDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            // Equipped titles for the roster (gamification rollup) — batch lookup.
            var rosterIds = viewModel.Students.Select(s => s.UserId).ToList();
            var titleKeys = await _gamificationStatsRepository.GetDisplayedTitleKeysByStudentIdsAsync(rosterIds);

            var dto = new CourseWithDetailsDto
            {
                Id = viewModel.Course.Id,
                Name = viewModel.Course.Name,
                Code = viewModel.Course.Code,
                Description = viewModel.Course.Description,
                UniversityId = viewModel.Course.UniversityId,
                UniversityName = viewModel.University?.Name,
                ExternalCourseId = viewModel.Course.ExternalCourseId,
                TitleTracks = viewModel.Course.TitleTracks,
                EnableEnem = viewModel.Course.EnableEnem,
                EnemArea = viewModel.Course.EnemArea,
                University = viewModel.University != null ? new UniversityDto
                {
                    Id = viewModel.University.Id,
                    Name = viewModel.University.Name,
                    Code = viewModel.University.Code,
                    Description = viewModel.University.Description,
                    HasAssignments = viewModel.University.HasAssignments,
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
                    LastName = s.LastName,
                    EquippedTitle = titleKeys.TryGetValue(s.UserId, out var tk) ? TitleCatalog.Resolve(tk) : null
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
            // Tenant isolation: verify caller can create courses in this university
            var callerUniversityId = GetCallerUniversityId();
            if (callerUniversityId != null && callerUniversityId.Value != request.UniversityId)
            {
                return NotFound(new { message = "University not found" });
            }

            // Plan enforcement: check course limit
            var university = await _universityRepository.GetByIdAsync(request.UniversityId);
            if (university != null)
            {
                // Check university-level override first (applies to ALL universities including enterprise)
                int? maxCourses = university.MaxCourses;

                // If no university-level override and NOT enterprise, check subscription plan
                if (maxCourses == null && !university.IsEnterprise)
                {
                    var subscription = await _subscriptionRepository.GetActiveByUniversityIdAsync(request.UniversityId);
                    if (subscription?.Plan != null)
                    {
                        maxCourses = subscription.Plan.MaxCourses;
                    }
                }

                if (maxCourses.HasValue)
                {
                    var existingCourses = await _courseRepository.GetByUniversityIdAsync(request.UniversityId);
                    if (existingCourses.Count() >= maxCourses.Value)
                    {
                        return StatusCode(403, new { message = $"Course limit reached ({maxCourses.Value}). Please upgrade your plan to create more courses." });
                    }
                }
            }

            var course = new Course
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                UniversityId = request.UniversityId,
                ExternalCourseId = request.ExternalCourseId,
                TitleTracks = string.IsNullOrWhiteSpace(request.TitleTracks) ? null : request.TitleTracks.Trim(),
                EnableEnem = request.EnableEnem,
                EnemArea = string.IsNullOrWhiteSpace(request.EnemArea) ? null : request.EnemArea.Trim()
            };

            var created = await _courseService.CreateAsync(course, _currentUserService.GetCurrentUser());

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
                ExternalCourseId = viewModel.Course.ExternalCourseId,
                TitleTracks = viewModel.Course.TitleTracks,
                EnableEnem = viewModel.Course.EnableEnem,
                EnemArea = viewModel.Course.EnemArea,
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
            // Tenant isolation: verify caller owns this course
            if (!await CallerOwnsCourseAsync(id))
            {
                return NotFound(new { message = "Course not found" });
            }

            var course = new Course
            {
                Name = request.Name ?? string.Empty,
                Code = request.Code ?? string.Empty,
                Description = request.Description,
                ExternalCourseId = request.ExternalCourseId,
                // Raw passthrough: null = leave unchanged, "" = clear (auto-detect), "math,.." = set
                TitleTracks = request.TitleTracks,
                EnableEnem = request.EnableEnem,
                EnemArea = string.IsNullOrWhiteSpace(request.EnemArea) ? null : request.EnemArea.Trim()
            };

            var viewModel = await _courseService.UpdateAsync(id, course, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Updated course {Name} with ID {Id}", viewModel.Course.Name, viewModel.Course.Id);

            var dto = new CourseDetailDto
            {
                Id = viewModel.Course.Id,
                Name = viewModel.Course.Name,
                Code = viewModel.Course.Code,
                Description = viewModel.Course.Description,
                UniversityId = viewModel.Course.UniversityId,
                UniversityName = viewModel.UniversityName,
                ExternalCourseId = viewModel.Course.ExternalCourseId,
                TitleTracks = viewModel.Course.TitleTracks,
                EnableEnem = viewModel.Course.EnableEnem,
                EnemArea = viewModel.Course.EnemArea,
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
            // Tenant isolation: verify caller owns this course
            if (!await CallerOwnsCourseAsync(id))
            {
                return NotFound(new { message = "Course not found" });
            }

            await _courseService.DeleteAsync(id, _currentUserService.GetCurrentUser());

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
            // Tenant isolation: verify caller owns this course
            if (!await CallerOwnsCourseAsync(courseId))
            {
                return NotFound(new { message = "Course not found" });
            }

            await _courseService.AssignProfessorAsync(courseId, professorId, _currentUserService.GetCurrentUser());

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
            // Tenant isolation: verify caller owns this course
            if (!await CallerOwnsCourseAsync(courseId))
            {
                return NotFound(new { message = "Course not found" });
            }

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
