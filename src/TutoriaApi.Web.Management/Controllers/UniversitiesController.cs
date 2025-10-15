using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/universities")]
[Authorize] // Default: All authenticated users can read
public class UniversitiesController : ControllerBase
{
    private readonly IUniversityRepository _universityRepository;
    private readonly IUniversityService _universityService;
    private readonly ILogger<UniversitiesController> _logger;

    public UniversitiesController(
        IUniversityRepository universityRepository,
        IUniversityService universityService,
        ILogger<UniversitiesController> logger)
    {
        _universityRepository = universityRepository;
        _universityService = universityService;
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
        var university = await _universityRepository.GetByIdWithCoursesAsync(id);

        if (university == null)
        {
            return NotFound(new { message = "University not found" });
        }

        var dto = new UniversityWithCoursesDto
        {
            Id = university.Id,
            Name = university.Name,
            Code = university.Code,
            Description = university.Description,
            CreatedAt = university.CreatedAt,
            UpdatedAt = university.UpdatedAt,
            Courses = university.Courses.Select(c => new CourseDto
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                Description = c.Description
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

        // Check if university with same name or code exists
        if (await _universityRepository.ExistsByNameAsync(request.Name))
        {
            return BadRequest(new { message = "University with this name already exists" });
        }

        if (await _universityRepository.ExistsByCodeAsync(request.Code))
        {
            return BadRequest(new { message = "University with this code already exists" });
        }

        var university = new University
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description
        };

        var created = await _universityRepository.AddAsync(university);

        _logger.LogInformation("Created university {Name} with ID {Id}", created.Name, created.Id);

        var dto = new UniversityDto
        {
            Id = created.Id,
            Name = created.Name,
            Code = created.Code,
            Description = created.Description,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        };

        return CreatedAtAction(nameof(GetUniversity), new { id = created.Id }, dto);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<UniversityDto>> UpdateUniversity(int id, [FromBody] UniversityUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var university = await _universityRepository.GetByIdAsync(id);
        if (university == null)
        {
            return NotFound(new { message = "University not found" });
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            // Check if name is taken by another university
            var existingByName = await _universityRepository.GetByNameAsync(request.Name);
            if (existingByName != null && existingByName.Id != id)
            {
                return BadRequest(new { message = "University with this name already exists" });
            }
            university.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            // Check if code is taken by another university
            var existingByCode = await _universityRepository.GetByCodeAsync(request.Code);
            if (existingByCode != null && existingByCode.Id != id)
            {
                return BadRequest(new { message = "University with this code already exists" });
            }
            university.Code = request.Code;
        }

        if (request.Description != null)
        {
            university.Description = request.Description;
        }

        await _universityRepository.UpdateAsync(university);

        _logger.LogInformation("Updated university {Name} with ID {Id}", university.Name, university.Id);

        var dto = new UniversityDto
        {
            Id = university.Id,
            Name = university.Name,
            Code = university.Code,
            Description = university.Description,
            CreatedAt = university.CreatedAt,
            UpdatedAt = university.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult> DeleteUniversity(int id)
    {
        var university = await _universityRepository.GetByIdAsync(id);
        if (university == null)
        {
            return NotFound(new { message = "University not found" });
        }

        await _universityRepository.DeleteAsync(university);

        _logger.LogInformation("Deleted university {Name} with ID {Id}", university.Name, university.Id);

        return Ok(new { message = "University deleted successfully" });
    }
}
