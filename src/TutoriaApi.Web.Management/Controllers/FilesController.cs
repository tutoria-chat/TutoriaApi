using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages file uploads and storage for module content with multi-tenant access control.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize(Policy = "ProfessorOrAbove")]
public class FilesController : BaseAuthController
{
    private readonly IFileService _fileService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileService fileService,
        ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<FileListDto>>> GetFiles(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? moduleId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var (items, total) = await _fileService.GetPagedFilesAsync(
                moduleId,
                search,
                page,
                size,
                currentUser);

            var dtos = items.Select(f => new FileListDto
            {
                Id = f.Id,
                Name = f.Name,
                FileType = f.FileType,
                FileName = f.FileName,
                BlobPath = f.BlobPath,
                BlobUrl = f.BlobUrl,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                ModuleId = f.ModuleId,
                ModuleName = f.Module?.Name,
                IsActive = f.IsActive,
                OpenAIFileId = f.OpenAIFileId,
                AnthropicFileId = f.AnthropicFileId,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }).ToList();

            return Ok(new PaginatedResponse<FileListDto>
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
            _logger.LogError(ex, "Error retrieving files");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FileDetailDto>> GetFile(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var viewModel = await _fileService.GetFileWithDetailsAsync(id, currentUser);

            if (viewModel == null)
            {
                return NotFound(new { message = "File not found" });
            }

            var file = viewModel.File;

            return Ok(new FileDetailDto
            {
                Id = file.Id,
                Name = file.Name,
                FileType = file.FileType,
                FileName = file.FileName,
                BlobPath = file.BlobPath,
                BlobUrl = file.BlobUrl,
                BlobContainer = file.BlobContainer,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                ModuleId = file.ModuleId,
                ModuleName = viewModel.ModuleName,
                CourseId = file.Module?.CourseId,
                CourseName = viewModel.CourseName,
                UniversityId = file.Module?.Course?.UniversityId,
                UniversityName = viewModel.UniversityName,
                IsActive = file.IsActive,
                OpenAIFileId = file.OpenAIFileId,
                AnthropicFileId = file.AnthropicFileId,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<FileDetailDto>> UploadFile([FromForm] UploadFileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            using var stream = request.File.OpenReadStream();
            var file = await _fileService.UploadFileAsync(
                request.ModuleId,
                stream,
                request.File.FileName,
                request.File.ContentType ?? "application/octet-stream",
                request.File.Length,
                request.Name,
                currentUser);

            _logger.LogInformation("Uploaded file {FileName} for module {ModuleId}", file.FileName, request.ModuleId);

            // Get full details for response
            var viewModel = await _fileService.GetFileWithDetailsAsync(file.Id, currentUser);

            return CreatedAtAction(nameof(GetFile), new { id = file.Id }, new FileDetailDto
            {
                Id = file.Id,
                FileName = file.FileName,
                BlobPath = file.BlobPath,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                ModuleId = file.ModuleId,
                ModuleName = viewModel?.ModuleName,
                CourseId = file.Module?.CourseId,
                CourseName = viewModel?.CourseName,
                UniversityId = file.Module?.Course?.UniversityId,
                UniversityName = viewModel?.UniversityName,
                OpenAIFileId = file.OpenAIFileId,
                IsActive = file.IsActive,
                // ErrorMessage removed: null,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized upload attempt to module {ModuleId}", request.ModuleId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} for module {ModuleId}", request.Name, request.ModuleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FileDetailDto>> UpdateFile(int id, [FromBody] UpdateFileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var file = await _fileService.UpdateFileAsync(id, request.FileName, currentUser);

            return Ok(new FileDetailDto
            {
                Id = file.Id,
                FileName = file.FileName,
                BlobPath = file.BlobPath,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                ModuleId = file.ModuleId,
                ModuleName = file.Module?.Name,
                OpenAIFileId = file.OpenAIFileId,
                IsActive = file.IsActive,
                // ErrorMessage removed: null,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized update attempt for file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFile(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _fileService.DeleteFileAsync(id, currentUser);

            _logger.LogInformation("Deleted file with ID {Id}", id);

            return Ok(new { message = "File deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized delete attempt for file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}/download")]
    public async Task<ActionResult> DownloadFile(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var downloadUrl = await _fileService.GetDownloadUrlAsync(id, currentUser);

            return Redirect(downloadUrl);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized download attempt for file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<FileDetailDto>> UpdateFileStatus(
        int id,
        [FromBody] UpdateFileStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var file = await _fileService.UpdateFileStatusAsync(
                id,
                request.IsActive ? "active" : "inactive",
                null, // ErrorMessage removed
                request.OpenAIFileId);

            _logger.LogInformation("Updated file status for {FileName} to {Status}", file.FileName, file.IsActive);

            return Ok(new FileDetailDto
            {
                Id = file.Id,
                FileName = file.FileName,
                BlobPath = file.BlobPath,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                ModuleId = file.ModuleId,
                ModuleName = file.Module?.Name,
                OpenAIFileId = file.OpenAIFileId,
                IsActive = file.IsActive,
                // ErrorMessage removed: null,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file status for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
