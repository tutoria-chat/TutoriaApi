using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

[ApiController]
[Route("api/universities")]
[Authorize] // Default: All authenticated users can read
public class UniversitiesController : ControllerBase
{
    private readonly IUniversityService _universityService;
    private readonly IStudentService _studentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UniversitiesController> _logger;

    public UniversitiesController(
        IUniversityService universityService,
        IStudentService studentService,
        ICurrentUserService currentUserService,
        ILogger<UniversitiesController> logger)
    {
        _universityService = universityService;
        _studentService = studentService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<UniversityDto>>> GetUniversities(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var (items, total) = await _universityService.GetPagedAsync(search, page, size);

        var dtos = items.Select(u => new UniversityDto
        {
            Id = u.Id,
            Name = u.Name,
            Code = u.Code,
            Description = u.Description,
            Address = u.Address,
            TaxId = u.TaxId,
            ContactEmail = u.ContactEmail,
            ContactPhone = u.ContactPhone,
            ContactPerson = u.ContactPerson,
            Website = u.Website,
            SubscriptionTier = u.SubscriptionTier,
            IsEnterprise = u.IsEnterprise,
            MaxCourses = u.MaxCourses,
            MaxModules = u.MaxModules,
            MaxStudents = u.MaxStudents,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return Ok(new PaginatedResponse<UniversityDto>
        {
            Items = dtos,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UniversityWithCoursesDto>> GetUniversity(int id)
    {
        var viewModel = await _universityService.GetUniversityWithDetailsAsync(id);

        if (viewModel == null)
        {
            return NotFound(new { message = "University not found" });
        }

        // Get student count for this university
        var studentsCount = await _studentService.GetStudentCountByUniversityAsync(viewModel.University.Id);

        // Map view model to DTO
        var dto = new UniversityWithCoursesDto
        {
            Id = viewModel.University.Id,
            Name = viewModel.University.Name,
            Code = viewModel.University.Code,
            Description = viewModel.University.Description,
            Address = viewModel.University.Address,
            // Individual address fields
            PostalCode = viewModel.University.PostalCode,
            Street = viewModel.University.Street,
            StreetNumber = viewModel.University.StreetNumber,
            Complement = viewModel.University.Complement,
            Neighborhood = viewModel.University.Neighborhood,
            City = viewModel.University.City,
            State = viewModel.University.State,
            Country = viewModel.University.Country,
            TaxId = viewModel.University.TaxId,
            ContactEmail = viewModel.University.ContactEmail,
            ContactPhone = viewModel.University.ContactPhone,
            ContactPerson = viewModel.University.ContactPerson,
            Website = viewModel.University.Website,
            SubscriptionTier = viewModel.University.SubscriptionTier,
            IsEnterprise = viewModel.University.IsEnterprise,
            MaxCourses = viewModel.University.MaxCourses,
            MaxModules = viewModel.University.MaxModules,
            MaxStudents = viewModel.University.MaxStudents,
            CreatedAt = viewModel.University.CreatedAt,
            UpdatedAt = viewModel.University.UpdatedAt,
            ProfessorsCount = viewModel.ProfessorsCount,
            CoursesCount = viewModel.Courses.Count,
            StudentsCount = studentsCount,
            Courses = viewModel.Courses.Select(c => new CourseDetailDto
            {
                Id = c.Course.Id,
                Name = c.Course.Name,
                Code = c.Course.Code,
                Description = c.Course.Description,
                UniversityId = c.Course.UniversityId,
                UniversityName = viewModel.University.Name,
                ModulesCount = c.ModulesCount,
                ProfessorsCount = c.ProfessorsCount,
                StudentsCount = c.StudentsCount,
                CreatedAt = c.Course.CreatedAt,
                UpdatedAt = c.Course.UpdatedAt
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<UniversityDto>> CreateUniversity([FromBody] UniversityCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // TODO: Refactor - validation logic should be in service
        try
        {
            var university = new University
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                Address = request.Address,
                // Individual address fields
                PostalCode = request.PostalCode,
                Street = request.Street,
                StreetNumber = request.StreetNumber,
                Complement = request.Complement,
                Neighborhood = request.Neighborhood,
                City = request.City,
                State = request.State,
                Country = request.Country,
                TaxId = request.TaxId,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                ContactPerson = request.ContactPerson,
                Website = request.Website,
                SubscriptionTier = request.SubscriptionTier,
                // Plan limits & enterprise config
                IsEnterprise = request.IsEnterprise,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
            };

            var created = await _universityService.CreateAsync(university, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Created university {Name} with ID {Id}", created.Name, created.Id);

            var dto = new UniversityDto
            {
                Id = created.Id,
                Name = created.Name,
                Code = created.Code,
                Description = created.Description,
                Address = created.Address,
                // Individual address fields
                PostalCode = created.PostalCode,
                Street = created.Street,
                StreetNumber = created.StreetNumber,
                Complement = created.Complement,
                Neighborhood = created.Neighborhood,
                City = created.City,
                State = created.State,
                Country = created.Country,
                TaxId = created.TaxId,
                ContactEmail = created.ContactEmail,
                ContactPhone = created.ContactPhone,
                ContactPerson = created.ContactPerson,
                Website = created.Website,
                SubscriptionTier = created.SubscriptionTier,
                IsEnterprise = created.IsEnterprise,
                MaxCourses = created.MaxCourses,
                MaxModules = created.MaxModules,
                MaxStudents = created.MaxStudents,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetUniversity), new { id = created.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating university");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<UniversityDto>> UpdateUniversity(int id, [FromBody] UniversityUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // TODO: Refactor to use service layer
        try
        {
            var updated = await _universityService.UpdateAsync(id, new University
            {
                Name = request.Name!,
                Code = request.Code!,
                Description = request.Description,
                Address = request.Address,
                // Individual address fields
                PostalCode = request.PostalCode,
                Street = request.Street,
                StreetNumber = request.StreetNumber,
                Complement = request.Complement,
                Neighborhood = request.Neighborhood,
                City = request.City,
                State = request.State,
                Country = request.Country,
                TaxId = request.TaxId,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                ContactPerson = request.ContactPerson,
                Website = request.Website,
                SubscriptionTier = request.SubscriptionTier ?? 3,
                // Plan limits & enterprise config
                IsEnterprise = request.IsEnterprise ?? false,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
            }, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Updated university {Name} with ID {Id}", updated.Name, updated.Id);

            return Ok(new UniversityDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Code = updated.Code,
                Description = updated.Description,
                Address = updated.Address,
                // Individual address fields
                PostalCode = updated.PostalCode,
                Street = updated.Street,
                StreetNumber = updated.StreetNumber,
                Complement = updated.Complement,
                Neighborhood = updated.Neighborhood,
                City = updated.City,
                State = updated.State,
                Country = updated.Country,
                TaxId = updated.TaxId,
                ContactEmail = updated.ContactEmail,
                ContactPhone = updated.ContactPhone,
                ContactPerson = updated.ContactPerson,
                Website = updated.Website,
                SubscriptionTier = updated.SubscriptionTier,
                IsEnterprise = updated.IsEnterprise,
                MaxCourses = updated.MaxCourses,
                MaxModules = updated.MaxModules,
                MaxStudents = updated.MaxStudents,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "University not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> DeleteUniversity(int id)
    {
        // TODO: Refactor to use service layer
        try
        {
            await _universityService.DeleteAsync(id, _currentUserService.GetCurrentUser());
            _logger.LogInformation("Deleted university with ID {Id}", id);
            return Ok(new { message = "University deleted successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "University not found" });
        }
    }
}
