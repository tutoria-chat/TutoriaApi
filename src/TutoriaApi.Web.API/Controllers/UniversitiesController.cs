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
    private readonly IUniversityPersonalizationService _personalizationService;
    private readonly IMajorService _majorService;
    private readonly ILogger<UniversitiesController> _logger;

    public UniversitiesController(
        IUniversityService universityService,
        IStudentService studentService,
        ICurrentUserService currentUserService,
        IUniversityPersonalizationService personalizationService,
        IMajorService majorService,
        ILogger<UniversitiesController> logger)
    {
        _universityService = universityService;
        _studentService = studentService;
        _currentUserService = currentUserService;
        _personalizationService = personalizationService;
        _majorService = majorService;
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

        // Tenant isolation: non-super-admin users only see their own university
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.UserType != "super_admin" && currentUser.UniversityId.HasValue)
        {
            var university = await _universityService.GetByIdAsync(currentUser.UniversityId.Value);
            if (university == null)
            {
                return Ok(new PaginatedResponse<UniversityDto>
                {
                    Items = new List<UniversityDto>(),
                    Total = 0, Page = page, Size = size, Pages = 0
                });
            }

            // Load personalization separately (GetByIdAsync doesn't eager-load it)
            var personalization = await _personalizationService.GetByUniversityIdAsync(university.Id);

            return Ok(new PaginatedResponse<UniversityDto>
            {
                Items = new List<UniversityDto> { MapToDto(university, personalization) },
                Total = 1, Page = 1, Size = size, Pages = 1
            });
        }

        var (items, total) = await _universityService.GetPagedAsync(search, page, size);

        // Personalization is already included via SearchAsync's Include(u => u.Personalization)
        var dtos = items.Select(u => MapToDto(u)).ToList();

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
        // Tenant isolation: non-super-admin users can only view their own university
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.UserType != "super_admin" && currentUser.UniversityId.HasValue && currentUser.UniversityId.Value != id)
        {
            return NotFound(new { message = "University not found" });
        }

        var viewModel = await _universityService.GetUniversityWithDetailsAsync(id);

        if (viewModel == null)
        {
            return NotFound(new { message = "University not found" });
        }

        // Get student count for this university
        var studentsCount = await _studentService.GetStudentCountByUniversityAsync(viewModel.University.Id);
        var majors = await _majorService.GetByUniversityAsync(viewModel.University.Id);

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
            HasAssignments = viewModel.University.HasAssignments,
            HasAIQuizzes = viewModel.University.HasAIQuizzes,
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
            }).ToList(),
            Majors = majors.Select(m => new MajorDto
            {
                Id = m.Id,
                UniversityId = m.UniversityId,
                Name = m.Name,
                CreatedAt = m.CreatedAt,
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<UniversityDto>> CreateUniversity([FromBody] UniversityCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var university = new University
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                Address = request.Address,
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
                IsEnterprise = request.IsEnterprise,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
            };

            var created = await _universityService.CreateAsync(university, _currentUserService.GetCurrentUser());
            _logger.LogInformation("Created university {Name} with ID {Id}", created.Name, created.Id);

            // Seed the standard Majors list so the institution starts with sensible defaults.
            try { await _majorService.SeedDefaultsAsync(created.Id); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to seed default majors for university {Id}", created.Id); }

            return CreatedAtAction(nameof(GetUniversity), new { id = created.Id }, MapToDto(created));
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
            return BadRequest(ModelState);

        try
        {
            var updated = await _universityService.UpdateAsync(id, new University
            {
                Name = request.Name!,
                Code = request.Code!,
                Description = request.Description,
                Address = request.Address,
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
                IsEnterprise = request.IsEnterprise ?? false,
                HasAssignments = request.HasAssignments ?? false,
                HasAIQuizzes = request.HasAIQuizzes ?? false,
                MaxCourses = request.MaxCourses,
                MaxModules = request.MaxModules,
                MaxStudents = request.MaxStudents,
            }, _currentUserService.GetCurrentUser());

            _logger.LogInformation("Updated university {Name} with ID {Id}", updated.Name, updated.Id);

            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "University not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> DeleteUniversity(int id)
    {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Get the institution's trusted web addresses (CORS allowlist).</summary>
    [HttpGet("{id}/trusted-origins")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> GetTrustedOrigins(int id)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var origins = await _universityService.GetAllowedOriginsAsync(id, currentUser);
            return Ok(new { origins });
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "University not found" }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trusted origins for university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Replace the institution's trusted web addresses (manager or super_admin).</summary>
    [HttpPut("{id}/trusted-origins")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> UpdateTrustedOrigins(int id, [FromBody] TrustedOriginsUpdateRequest request)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var origins = await _universityService.UpdateAllowedOriginsAsync(id, request.Origins ?? new(), currentUser);
            return Ok(new { origins });
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "University not found" }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trusted origins for university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Get widget personalization for a university (public — used by widget)</summary>
    [HttpGet("{id}/personalization")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPersonalization(int id)
    {
        try
        {
            var p = await _personalizationService.GetByUniversityIdAsync(id);
            if (p == null)
                return Ok(new { primaryColor = (string?)null, secondaryColor = (string?)null, defaultTheme = "auto", bubbleOpacity = (int?)null });

            return Ok(new
            {
                primaryColor = p.PrimaryColor,
                secondaryColor = p.SecondaryColor,
                defaultTheme = p.DefaultTheme,
                bubbleOpacity = p.BubbleOpacity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personalization for university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>Upsert widget personalization for a university (manager or super_admin)</summary>
    [HttpPut("{id}/personalization")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> UpsertPersonalization(int id, [FromBody] UniversityAppearanceUpdateRequest request)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var result = await _personalizationService.UpsertAsync(
                id,
                request.WidgetPrimaryColor,
                request.WidgetSecondaryColor,
                request.WidgetDefaultTheme,
                request.WidgetBubbleOpacity,
                currentUser.UserId,
                currentUser.UserType,
                currentUser.UniversityId);

            return Ok(new
            {
                primaryColor = result.PrimaryColor,
                secondaryColor = result.SecondaryColor,
                defaultTheme = result.DefaultTheme,
                bubbleOpacity = result.BubbleOpacity,
                updatedAt = result.UpdatedAt
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "University not found" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting personalization for university {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps a University entity to a UniversityDto.
    /// Pass <paramref name="personalization"/> explicitly when the entity was fetched without
    /// eager-loading the Personalization navigation property (e.g. GetByIdAsync).
    /// When fetched via SearchAsync (which includes Personalization), omit the parameter.
    /// </summary>
    private static UniversityDto MapToDto(University u, UniversityPersonalization? personalization = null)
    {
        var p = personalization ?? u.Personalization;
        return new UniversityDto
        {
            Id = u.Id,
            Name = u.Name,
            Code = u.Code,
            Description = u.Description,
            Address = u.Address,
            PostalCode = u.PostalCode,
            Street = u.Street,
            StreetNumber = u.StreetNumber,
            Complement = u.Complement,
            Neighborhood = u.Neighborhood,
            City = u.City,
            State = u.State,
            Country = u.Country,
            TaxId = u.TaxId,
            ContactEmail = u.ContactEmail,
            ContactPhone = u.ContactPhone,
            ContactPerson = u.ContactPerson,
            Website = u.Website,
            SubscriptionTier = u.SubscriptionTier,
            IsEnterprise = u.IsEnterprise,
            HasAssignments = u.HasAssignments,
            HasAIQuizzes = u.HasAIQuizzes,
            MaxCourses = u.MaxCourses,
            MaxModules = u.MaxModules,
            MaxStudents = u.MaxStudents,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            WidgetPrimaryColor = p?.PrimaryColor,
            WidgetSecondaryColor = p?.SecondaryColor,
            WidgetDefaultTheme = p?.DefaultTheme,
            WidgetBubbleOpacity = p?.BubbleOpacity,
        };
    }
}
