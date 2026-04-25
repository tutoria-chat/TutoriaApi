using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (moduleId <= 0) return BadRequest(new { message = "moduleId is required" });
        if (page < 1) page = 1;
        if (size < 1) size = 20;
        if (size > 100) size = 100;

        try
        {
            var (items, total) = await _assignmentService.GetPagedAsync(
                moduleId, page, size, _currentUserService.GetCurrentUser());

            var dtos = items.Select(a => new AssignmentListDto
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
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            }).ToList();

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
            _logger.LogError(ex, "Error retrieving assignments for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

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
    [RequestSizeLimit(62914560)] // 60 MB to accommodate two files
    [RequestFormLimits(MultipartBodyLengthLimit = 62914560)]
    public async Task<ActionResult<AssignmentDetailDto>> CreateAssignment([FromForm] AssignmentCreateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var allowedTypes = new[] { "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        if (!allowedTypes.Contains(request.File.ContentType))
            return BadRequest(new { message = "Only PDF and DOCX files are allowed for assignments" });

        if (request.RubricFile != null && !allowedTypes.Contains(request.RubricFile.ContentType))
            return BadRequest(new { message = "Only PDF and DOCX files are allowed for the rubric" });

        try
        {
            using var stream = request.File.OpenReadStream();
            Stream? rubricStream = request.RubricFile != null ? request.RubricFile.OpenReadStream() : null;

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
                _currentUserService.GetCurrentUser());

            rubricStream?.Dispose();

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
