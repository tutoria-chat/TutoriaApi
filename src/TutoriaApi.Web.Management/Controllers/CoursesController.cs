using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IProfessorRepository _professorRepository;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseRepository courseRepository,
        IUniversityRepository universityRepository,
        IProfessorRepository professorRepository,
        TutoriaDbContext context,
        ILogger<CoursesController> logger)
    {
        _courseRepository = courseRepository;
        _universityRepository = universityRepository;
        _professorRepository = professorRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<CourseDetailDto>>> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? universityId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = _context.Courses.AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(c => c.UniversityId == universityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
        }

        var total = await query.CountAsync();
        var courses = await query
            .Include(c => c.University)
            .OrderBy(c => c.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = new List<CourseDetailDto>();
        foreach (var course in courses)
        {
            var modulesCount = await _context.Modules.CountAsync(m => m.CourseId == course.Id);
            var professorsCount = await _context.ProfessorCourses.CountAsync(pc => pc.CourseId == course.Id);
            var studentsCount = await _context.Students.CountAsync(s => s.CourseId == course.Id);

            items.Add(new CourseDetailDto
            {
                Id = course.Id,
                Name = course.Name,
                Code = course.Code,
                Description = course.Description,
                UniversityId = course.UniversityId,
                UniversityName = course.University?.Name,
                ModulesCount = modulesCount,
                ProfessorsCount = professorsCount,
                StudentsCount = studentsCount,
                CreatedAt = course.CreatedAt,
                UpdatedAt = course.UpdatedAt
            });
        }

        return Ok(new PaginatedResponse<CourseDetailDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CourseWithDetailsDto>> GetCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.University)
            .Include(c => c.Modules)
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        var dto = new CourseWithDetailsDto
        {
            Id = course.Id,
            Name = course.Name,
            Code = course.Code,
            Description = course.Description,
            UniversityId = course.UniversityId,
            University = course.University != null ? new UniversityDto
            {
                Id = course.University.Id,
                Name = course.University.Name,
                Code = course.University.Code,
                Description = course.University.Description,
                CreatedAt = course.University.CreatedAt,
                UpdatedAt = course.University.UpdatedAt
            } : null,
            Modules = course.Modules.Select(m => new ModuleDto
            {
                Id = m.Id,
                Name = m.Name,
                Code = m.Code,
                Description = m.Description,
                Semester = m.Semester,
                Year = m.Year
            }).ToList(),
            Students = course.Students.Select(s => new StudentDto
            {
                Id = s.Id,
                Username = s.Username,
                Email = s.Email,
                FirstName = s.FirstName,
                LastName = s.LastName
            }).ToList(),
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<CourseDetailDto>> CreateCourse([FromBody] CourseCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if university exists
        var university = await _universityRepository.GetByIdAsync(request.UniversityId);
        if (university == null)
        {
            return NotFound(new { message = "University not found" });
        }

        // Check if course with same code exists in university
        var existingCourse = await _context.Courses
            .FirstOrDefaultAsync(c => c.Code == request.Code && c.UniversityId == request.UniversityId);

        if (existingCourse != null)
        {
            return BadRequest(new { message = "Course with this code already exists in this university" });
        }

        var course = new Course
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            UniversityId = request.UniversityId
        };

        var created = await _courseRepository.AddAsync(course);

        _logger.LogInformation("Created course {Name} with ID {Id}", created.Name, created.Id);

        return CreatedAtAction(nameof(GetCourse), new { id = created.Id }, new CourseDetailDto
        {
            Id = created.Id,
            Name = created.Name,
            Code = created.Code,
            Description = created.Description,
            UniversityId = created.UniversityId,
            UniversityName = university.Name,
            ModulesCount = 0,
            ProfessorsCount = 0,
            StudentsCount = 0,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<CourseDetailDto>> UpdateCourse(int id, [FromBody] CourseUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            course.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            // Check if code is taken by another course in same university
            var existingCourse = await _context.Courses
                .FirstOrDefaultAsync(c => c.Code == request.Code && c.UniversityId == course.UniversityId && c.Id != id);

            if (existingCourse != null)
            {
                return BadRequest(new { message = "Course with this code already exists in this university" });
            }

            course.Code = request.Code;
        }

        if (request.Description != null)
        {
            course.Description = request.Description;
        }

        await _courseRepository.UpdateAsync(course);

        _logger.LogInformation("Updated course {Name} with ID {Id}", course.Name, course.Id);

        var university = await _universityRepository.GetByIdAsync(course.UniversityId);

        return Ok(new CourseDetailDto
        {
            Id = course.Id,
            Name = course.Name,
            Code = course.Code,
            Description = course.Description,
            UniversityId = course.UniversityId,
            UniversityName = university?.Name,
            ModulesCount = await _context.Modules.CountAsync(m => m.CourseId == course.Id),
            ProfessorsCount = await _context.ProfessorCourses.CountAsync(pc => pc.CourseId == course.Id),
            StudentsCount = await _context.Students.CountAsync(s => s.CourseId == course.Id),
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> DeleteCourse(int id)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        await _courseRepository.DeleteAsync(course);

        _logger.LogInformation("Deleted course {Name} with ID {Id}", course.Name, course.Id);

        return Ok(new { message = "Course deleted successfully" });
    }

    [HttpPost("{courseId}/professors/{professorId}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> AssignProfessorToCourse(int courseId, int professorId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        var professor = await _professorRepository.GetByIdAsync(professorId);
        if (professor == null)
        {
            return NotFound(new { message = "Professor not found" });
        }

        // Check if assignment already exists
        var existing = await _context.ProfessorCourses
            .FirstOrDefaultAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);

        if (existing != null)
        {
            return BadRequest(new { message = "Professor is already assigned to this course" });
        }

        var assignment = new ProfessorCourse
        {
            ProfessorId = professorId,
            CourseId = courseId
        };

        _context.ProfessorCourses.Add(assignment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Assigned professor {ProfessorId} to course {CourseId}", professorId, courseId);

        return Ok(new { message = "Professor assigned to course successfully" });
    }

    [HttpDelete("{courseId}/professors/{professorId}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> UnassignProfessorFromCourse(int courseId, int professorId)
    {
        var assignment = await _context.ProfessorCourses
            .FirstOrDefaultAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);

        if (assignment == null)
        {
            return NotFound(new { message = "Professor is not assigned to this course" });
        }

        _context.ProfessorCourses.Remove(assignment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Unassigned professor {ProfessorId} from course {CourseId}", professorId, courseId);

        return Ok(new { message = "Professor unassigned from course successfully" });
    }
}
