using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize(Policy = "ProfessorOrAbove")]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(
        IAssignmentService assignmentService,
        ICurrentUserService currentUserService,
        ILogger<AssignmentsController> logger)
    {
        _assignmentService = assignmentService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AssignmentListDto>>> GetAssignments(
        [FromQuery] int moduleId = 0,
        [FromQuery] int courseId = 0,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (moduleId <= 0 && courseId <= 0)
            return BadRequest(new { message = "Either moduleId or courseId is required" });

        if (page < 1) page = 1;
        if (size < 1) size = 20;
        if (size > 100) size = 100;

        var currentUser = _currentUserService.GetCurrentUser();

        try
        {
            // Course-level query: published assignments from all modules in the course
            if (courseId > 0)
            {
                var courseItems = await _assignmentService.GetPublishedByCourseAsync(courseId, currentUser);
                var courseDtos = courseItems.Select(a => MapToListDto(a, includeModuleName: true)).ToList();
                return Ok(new PaginatedResponse<AssignmentListDto>
                {
                    Items = courseDtos,
                    Total = courseDtos.Count,
                    Page = 1,
                    Size = courseDtos.Count,
                    Pages = 1,
                });
            }

            // Module-level query: all assignments (incl. unpublished) for the owning module
            var (items, total) = await _assignmentService.GetPagedAsync(moduleId, page, size, currentUser);
            var dtos = items.Select(a => MapToListDto(a)).ToList();

            return Ok(new PaginatedResponse<AssignmentListDto>
            {
                Items = dtos,
                Total = total,
                Page = page,
                Size = size,
                Pages = (int)Math.Ceiling(total / (double)size),
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assignments (moduleId={ModuleId}, courseId={CourseId})", moduleId, courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private static AssignmentListDto MapToListDto(Assignment a, bool includeModuleName = false) => new()
    {
        Id = a.Id,
        ModuleId = a.ModuleId,
        ModuleName = includeModuleName ? a.Module?.Name : null,
        Title = a.Title,
        Description = a.Description,
        DueDate = a.DueDate,
        IsPublished = a.IsPublished,
        IsActive = a.IsActive,
        OriginalFileName = a.OriginalFileName,
        FileSizeBytes = a.FileSizeBytes,
        ContentType = a.ContentType,
        Keywords = (a.Keywords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        RubricOriginalFileName = a.RubricOriginalFileName,
        CreatedByUserId = a.CreatedByUserId,
        ContextFiles = a.ContextFiles.Select(cf => new AssignmentContextFileDto
        {
            Id = cf.Id,
            OriginalFileName = cf.OriginalFileName,
            FileSizeBytes = cf.FileSizeBytes,
            ContentType = cf.ContentType,
        }).ToList(),
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };

    [HttpGet("{id}")]
    public async Task<ActionResult<AssignmentDetailDto>> GetAssignment(int id)
    {
        try
        {
            var result = await _assignmentService.GetByIdAsync(id, _currentUserService.GetCurrentUser());
            if (result == null) return NotFound(new { message = "Assignment not found" });

            var a = result.Assignment;
            return Ok(new AssignmentDetailDto
            {
                Id = a.Id,
                ModuleId = a.ModuleId,
                Title = a.Title,
                Description = a.Description,
                DueDate = a.DueDate,
                IsPublished = a.IsPublished,
                IsActive = a.IsActive,
                OriginalFileName = a.OriginalFileName,
                FileSizeBytes = a.FileSizeBytes,
                ContentType = a.ContentType,
                Keywords = (a.Keywords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                RubricOriginalFileName = a.RubricOriginalFileName,
                CreatedByUserId = a.CreatedByUserId,
                DownloadUrl = result.DownloadUrl,
                RubricDownloadUrl = result.RubricDownloadUrl,
                ContextFiles = result.ContextFiles.Select(cf => new AssignmentContextFileDto
                {
                    Id = cf.File.Id,
                    OriginalFileName = cf.File.OriginalFileName,
                    FileSizeBytes = cf.File.FileSizeBytes,
                    ContentType = cf.File.ContentType,
                    DownloadUrl = cf.DownloadUrl,
                }).ToList(),
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assignment {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    [RequestSizeLimit(209715200)] // 200 MB to accommodate multiple context files
    [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
    public async Task<ActionResult<AssignmentDetailDto>> CreateAssignment([FromForm] AssignmentCreateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var allowedTypes = new[] { "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        if (!allowedTypes.Contains(request.File.ContentType))
            return BadRequest(new { message = "Only PDF and DOCX files are allowed for assignments" });

        if (request.RubricFile != null && !allowedTypes.Contains(request.RubricFile.ContentType))
            return BadRequest(new { message = "Only PDF and DOCX files are allowed for the rubric" });

        if (request.ContextFiles != null)
        {
            var invalidContextFile = request.ContextFiles.FirstOrDefault(f => !allowedTypes.Contains(f.ContentType));
            if (invalidContextFile != null)
                return BadRequest(new { message = $"Context file '{invalidContextFile.FileName}' must be a PDF or DOCX" });
        }

        try
        {
            using var stream = request.File.OpenReadStream();
            Stream? rubricStream = request.RubricFile != null ? request.RubricFile.OpenReadStream() : null;

            List<ContextFileUpload>? contextUploads = null;
            if (request.ContextFiles?.Count > 0)
            {
                contextUploads = request.ContextFiles
                    .Select(f => new ContextFileUpload(f.OpenReadStream(), f.FileName, f.ContentType, f.Length))
                    .ToList();
            }

            var assignment = await _assignmentService.CreateAsync(
                request.ModuleId,
                request.Title,
                request.Description,
                request.DueDate,
                request.Keywords,
                stream,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                rubricStream,
                request.RubricFile?.FileName,
                request.RubricFile?.ContentType,
                request.RubricFile?.Length,
                _currentUserService.GetCurrentUser(),
                contextUploads);

            rubricStream?.Dispose();
            if (contextUploads != null)
                foreach (var cu in contextUploads) await cu.Stream.DisposeAsync();

            return CreatedAtAction(nameof(GetAssignment), new { id = assignment.Id }, new AssignmentDetailDto
            {
                Id = assignment.Id,
                ModuleId = assignment.ModuleId,
                Title = assignment.Title,
                Description = assignment.Description,
                DueDate = assignment.DueDate,
                IsPublished = assignment.IsPublished,
                IsActive = assignment.IsActive,
                OriginalFileName = assignment.OriginalFileName,
                FileSizeBytes = assignment.FileSizeBytes,
                ContentType = assignment.ContentType,
                Keywords = (assignment.Keywords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                RubricOriginalFileName = assignment.RubricOriginalFileName,
                CreatedByUserId = assignment.CreatedByUserId,
                ContextFiles = [],
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt,
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assignment for module {ModuleId}", request.ModuleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AssignmentDetailDto>> UpdateAssignment(int id, [FromBody] AssignmentUpdateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var assignment = await _assignmentService.UpdateAsync(
                id, request.Title, request.Description, request.DueDate,
                request.Keywords, _currentUserService.GetCurrentUser());

            return Ok(new AssignmentDetailDto
            {
                Id = assignment.Id,
                ModuleId = assignment.ModuleId,
                Title = assignment.Title,
                Description = assignment.Description,
                DueDate = assignment.DueDate,
                IsPublished = assignment.IsPublished,
                IsActive = assignment.IsActive,
                OriginalFileName = assignment.OriginalFileName,
                FileSizeBytes = assignment.FileSizeBytes,
                ContentType = assignment.ContentType,
                Keywords = (assignment.Keywords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                RubricOriginalFileName = assignment.RubricOriginalFileName,
                CreatedByUserId = assignment.CreatedByUserId,
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating assignment {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAssignment(int id)
    {
        try
        {
            await _assignmentService.DeleteAsync(id, _currentUserService.GetCurrentUser());
            return Ok(new { message = "Assignment deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assignment {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<ActionResult<AssignmentDetailDto>> TogglePublish(int id)
    {
        try
        {
            var assignment = await _assignmentService.TogglePublishAsync(id, _currentUserService.GetCurrentUser());
            return Ok(new AssignmentDetailDto
            {
                Id = assignment.Id,
                ModuleId = assignment.ModuleId,
                Title = assignment.Title,
                Description = assignment.Description,
                DueDate = assignment.DueDate,
                IsPublished = assignment.IsPublished,
                IsActive = assignment.IsActive,
                OriginalFileName = assignment.OriginalFileName,
                FileSizeBytes = assignment.FileSizeBytes,
                ContentType = assignment.ContentType,
                Keywords = (assignment.Keywords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                RubricOriginalFileName = assignment.RubricOriginalFileName,
                CreatedByUserId = assignment.CreatedByUserId,
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling publish for assignment {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
