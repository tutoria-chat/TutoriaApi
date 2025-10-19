using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages academic modules (course units) and their configuration.
/// </summary>
/// <remarks>
/// Modules are individual units within a course (e.g., "Introduction to Python", "Calculus I").
/// Each module has an associated OpenAI Assistant and vector store for AI tutoring functionality.
///
/// **Authorization**: All endpoints require authentication. Write operations require ProfessorOrAbove policy.
///
/// **Key Features**:
/// - Module creation with system prompt and language configuration
/// - OpenAI Assistant and Vector Store integration
/// - File attachments for module content
/// - Module Access Tokens for widget integration
/// - Prompt improvement tracking
/// </remarks>
[ApiController]
[Route("api/modules")]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly IModuleRepository _moduleRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        TutoriaDbContext context,
        ILogger<ModulesController> logger)
    {
        _moduleRepository = moduleRepository;
        _courseRepository = courseRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ModuleListDto>>> GetModules(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? courseId = null,
        [FromQuery] int? semester = null,
        [FromQuery] int? year = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = _context.Modules
            .Include(m => m.Course)
            .AsQueryable();

        // Apply professor-scoped filtering
        var userType = User.FindFirst("type")?.Value;
        var isAdmin = User.FindFirst("isAdmin")?.Value?.ToLower() == "true";
        var userIdClaim = User.FindFirst("user_id")?.Value;

        if (userType == "professor" && !isAdmin && int.TryParse(userIdClaim, out var professorId))
        {
            // Non-admin professors can only see modules from courses they're assigned to
            query = query.Where(m => _context.ProfessorCourses
                .Any(pc => pc.ProfessorId == professorId && pc.CourseId == m.CourseId));
        }
        else if (userType == "professor" && isAdmin)
        {
            // Admin professors can see all modules in their university
            var universityIdClaim = User.FindFirst("university_id")?.Value;
            if (int.TryParse(universityIdClaim, out var universityId))
            {
                query = query.Where(m => m.Course.UniversityId == universityId);
            }
        }
        // Super admins see all modules (no filtering)

        if (courseId.HasValue)
        {
            query = query.Where(m => m.CourseId == courseId.Value);
        }

        if (semester.HasValue)
        {
            query = query.Where(m => m.Semester == semester.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Name.Contains(search) || m.Code.Contains(search));
        }

        var total = await query.CountAsync();

        // Use projection to avoid N+1 queries - count related entities in single query
        var items = await query
            .OrderBy(m => m.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(m => new ModuleListDto
            {
                Id = m.Id,
                Name = m.Name,
                Code = m.Code,
                Description = m.Description,
                Semester = m.Semester,
                Year = m.Year,
                CourseId = m.CourseId,
                CourseName = m.Course != null ? m.Course.Name : null,
                AIModelId = m.AIModelId,
                AIModelDisplayName = m.AIModel != null ? m.AIModel.DisplayName : null,
                FilesCount = _context.Files.Count(f => f.ModuleId == m.Id),
                TokensCount = _context.ModuleAccessTokens.Count(t => t.ModuleId == m.Id),
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToListAsync();

        return Ok(new PaginatedResponse<ModuleListDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModuleDetailDto>> GetModule(int id)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
                .ThenInclude(c => c.University)
            .Include(m => m.Files)
            .Include(m => m.AIModel)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
        {
            return NotFound(new { message = "Module not found" });
        }

        var dto = new ModuleDetailDto
        {
            Id = module.Id,
            Name = module.Name,
            Code = module.Code,
            Description = module.Description,
            SystemPrompt = module.SystemPrompt,
            Semester = module.Semester,
            Year = module.Year,
            CourseId = module.CourseId,
            Course = module.Course != null ? new CourseDto
            {
                Id = module.Course.Id,
                Name = module.Course.Name,
                Code = module.Course.Code,
                Description = module.Course.Description
            } : null,
            AIModelId = module.AIModelId,
            AIModel = module.AIModel != null ? new AIModelDto
            {
                Id = module.AIModel.Id,
                ModelName = module.AIModel.ModelName,
                DisplayName = module.AIModel.DisplayName,
                Provider = module.AIModel.Provider,
                MaxTokens = module.AIModel.MaxTokens,
                SupportsVision = module.AIModel.SupportsVision,
                SupportsFunctionCalling = module.AIModel.SupportsFunctionCalling,
                IsActive = module.AIModel.IsActive,
                IsDeprecated = module.AIModel.IsDeprecated
            } : null,
            OpenAIAssistantId = module.OpenAIAssistantId,
            OpenAIVectorStoreId = module.OpenAIVectorStoreId,
            LastPromptImprovedAt = module.LastPromptImprovedAt,
            PromptImprovementCount = module.PromptImprovementCount,
            TutorLanguage = module.TutorLanguage,
            Files = module.Files.Select(f => new FileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                BlobName = f.BlobName,
                ContentType = f.ContentType,
                Size = f.Size,
                OpenAIFileId = f.OpenAIFileId,
                Status = f.Status,
                CreatedAt = f.CreatedAt
            }).ToList(),
            CreatedAt = module.CreatedAt,
            UpdatedAt = module.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<ModuleDetailDto>> CreateModule([FromBody] ModuleCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if course exists
        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        // Check if module with same code exists in course
        var existingModule = await _context.Modules
            .FirstOrDefaultAsync(m => m.Code == request.Code && m.CourseId == request.CourseId);

        if (existingModule != null)
        {
            return BadRequest(new { message = "Module with this code already exists in this course" });
        }

        var module = new Module
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            SystemPrompt = request.SystemPrompt,
            Semester = request.Semester,
            Year = request.Year,
            CourseId = request.CourseId,
            AIModelId = request.AIModelId,
            TutorLanguage = request.TutorLanguage,
            PromptImprovementCount = 0
        };

        var created = await _moduleRepository.AddAsync(module);

        _logger.LogInformation("Created module {Name} with ID {Id}", created.Name, created.Id);

        return CreatedAtAction(nameof(GetModule), new { id = created.Id }, new ModuleDetailDto
        {
            Id = created.Id,
            Name = created.Name,
            Code = created.Code,
            Description = created.Description,
            SystemPrompt = created.SystemPrompt,
            Semester = created.Semester,
            Year = created.Year,
            CourseId = created.CourseId,
            TutorLanguage = created.TutorLanguage,
            PromptImprovementCount = created.PromptImprovementCount,
            Files = new List<FileDto>(),
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<ModuleDetailDto>> UpdateModule(int id, [FromBody] ModuleUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var module = await _moduleRepository.GetByIdAsync(id);
        if (module == null)
        {
            return NotFound(new { message = "Module not found" });
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            module.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            // Check if code is taken by another module in same course
            var existingModule = await _context.Modules
                .FirstOrDefaultAsync(m => m.Code == request.Code && m.CourseId == module.CourseId && m.Id != id);

            if (existingModule != null)
            {
                return BadRequest(new { message = "Module with this code already exists in this course" });
            }

            module.Code = request.Code;
        }

        if (request.Description != null)
        {
            module.Description = request.Description;
        }

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            module.SystemPrompt = request.SystemPrompt;
        }

        if (request.Semester.HasValue)
        {
            module.Semester = request.Semester;
        }

        if (request.Year.HasValue)
        {
            module.Year = request.Year;
        }

        if (!string.IsNullOrWhiteSpace(request.TutorLanguage))
        {
            module.TutorLanguage = request.TutorLanguage;
        }

        if (request.AIModelId.HasValue)
        {
            module.AIModelId = request.AIModelId;
        }

        await _moduleRepository.UpdateAsync(module);

        _logger.LogInformation("Updated module {Name} with ID {Id}", module.Name, module.Id);

        var course = await _courseRepository.GetByIdAsync(module.CourseId);

        return Ok(new ModuleDetailDto
        {
            Id = module.Id,
            Name = module.Name,
            Code = module.Code,
            Description = module.Description,
            SystemPrompt = module.SystemPrompt,
            Semester = module.Semester,
            Year = module.Year,
            CourseId = module.CourseId,
            Course = course != null ? new CourseDto
            {
                Id = course.Id,
                Name = course.Name,
                Code = course.Code,
                Description = course.Description
            } : null,
            OpenAIAssistantId = module.OpenAIAssistantId,
            OpenAIVectorStoreId = module.OpenAIVectorStoreId,
            LastPromptImprovedAt = module.LastPromptImprovedAt,
            PromptImprovementCount = module.PromptImprovementCount,
            TutorLanguage = module.TutorLanguage,
            Files = new List<FileDto>(),
            CreatedAt = module.CreatedAt,
            UpdatedAt = module.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult> DeleteModule(int id)
    {
        var module = await _moduleRepository.GetByIdAsync(id);
        if (module == null)
        {
            return NotFound(new { message = "Module not found" });
        }

        await _moduleRepository.DeleteAsync(module);

        _logger.LogInformation("Deleted module {Name} with ID {Id}", module.Name, module.Id);

        return Ok(new { message = "Module deleted successfully" });
    }

}
