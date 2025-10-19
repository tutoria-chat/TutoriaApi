using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Helpers;
using TutoriaApi.Web.Management.DTOs;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages file uploads and storage for module content with multi-tenant access control.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize(Policy = "ProfessorOrAbove")]
public class FilesController : ControllerBase
{
    private readonly TutoriaDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly AccessControlHelper _accessControl;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        TutoriaDbContext context,
        IBlobStorageService blobStorageService,
        AccessControlHelper accessControl,
        ILogger<FilesController> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _accessControl = accessControl;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private async Task<Core.Entities.User?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.Users.FindAsync(userId);
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

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var query = _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

        // Access control based on professor type
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                // Non-admin professors can only see files from courses they're assigned to
                var professorCourseIds = await _accessControl.GetProfessorCourseIdsAsync(currentUser.UserId);
                query = query.Where(f => professorCourseIds.Contains(f.Module.CourseId));
            }
            else
            {
                // Admin professors can see all files in their university
                query = query.Where(f => f.Module.Course.UniversityId == currentUser.UniversityId);
            }
        }

        if (moduleId.HasValue)
        {
            query = query.Where(f => f.ModuleId == moduleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(f => f.FileName.Contains(search));
        }

        var total = await query.CountAsync();
        var files = await query
            .OrderBy(f => f.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = files.Select(f => new FileListDto
        {
            Id = f.Id,
            FileName = f.FileName,
            BlobName = f.BlobName,
            ContentType = f.ContentType,
            Size = f.Size,
            ModuleId = f.ModuleId,
            ModuleName = f.Module?.Name,
            OpenAIFileId = f.OpenAIFileId,
            Status = f.Status,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();

        return Ok(new PaginatedResponse<FileListDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Size = size,
            Pages = (int)Math.Ceiling(total / (double)size)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FileDetailDto>> GetFile(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var file = await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        // Access control
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                // Non-admin professors must be assigned to the course
                var hasAccess = await _accessControl.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId,
                    file.Module.CourseId);

                if (!hasAccess)
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin professors can only access files in their university
                if (file.Module.Course.UniversityId != currentUser.UniversityId)
                {
                    return Forbid();
                }
            }
        }

        return Ok(new FileDetailDto
        {
            Id = file.Id,
            FileName = file.FileName,
            BlobName = file.BlobName,
            ContentType = file.ContentType,
            Size = file.Size,
            ModuleId = file.ModuleId,
            ModuleName = file.Module?.Name,
            CourseId = file.Module?.CourseId,
            CourseName = file.Module?.Course?.Name,
            UniversityId = file.Module?.Course?.UniversityId,
            UniversityName = file.Module?.Course?.University?.Name,
            OpenAIFileId = file.OpenAIFileId,
            Status = file.Status,
            ErrorMessage = file.ErrorMessage,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<FileDetailDto>> UploadFile(
        [FromForm] int moduleId,
        [FromForm] string? name,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Verify module exists
        var module = await _context.Modules
            .Include(m => m.Course)
                .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module == null)
        {
            return NotFound(new { message = "Module not found" });
        }

        // Access control - non-admin professors can only upload to courses they're assigned to
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                var hasAccess = await _accessControl.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId,
                    module.CourseId);

                if (!hasAccess)
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin professors can only access modules in their university
                if (module.Course.UniversityId != currentUser.UniversityId)
                {
                    return Forbid();
                }
            }
        }

        // Sanitize filename
        var sanitizedFilename = FileHelper.SanitizeFilename(file.FileName);
        if (string.IsNullOrWhiteSpace(sanitizedFilename))
        {
            return BadRequest(new { message = "Invalid filename" });
        }

        // Sanitize display name
        var sanitizedName = string.IsNullOrWhiteSpace(name)
            ? sanitizedFilename
            : FileHelper.SanitizeFilename(name);

        // Get file extension
        var fileExtension = FileHelper.GetFileExtension(sanitizedFilename);

        // Generate blob path
        var blobPath = _blobStorageService.GenerateBlobPath(
            module.Course.UniversityId,
            module.CourseId,
            moduleId,
            sanitizedFilename
        );

        try
        {
            // Upload to blob storage
            string blobUrl;
            using (var stream = file.OpenReadStream())
            {
                blobUrl = await _blobStorageService.UploadFileAsync(
                    stream,
                    blobPath,
                    file.ContentType ?? "application/octet-stream"
                );
            }

            // Create database record
            var fileEntity = new FileEntity
            {
                FileName = sanitizedName,
                BlobName = blobPath,
                ContentType = file.ContentType ?? "application/octet-stream",
                Size = file.Length,
                ModuleId = moduleId,
                Status = "pending"
            };

            _context.Files.Add(fileEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded file {FileName} for module {ModuleId}", sanitizedName, moduleId);

            return CreatedAtAction(nameof(GetFile), new { id = fileEntity.Id }, new FileDetailDto
            {
                Id = fileEntity.Id,
                FileName = fileEntity.FileName,
                BlobName = fileEntity.BlobName,
                ContentType = fileEntity.ContentType,
                Size = fileEntity.Size,
                ModuleId = fileEntity.ModuleId,
                ModuleName = module.Name,
                CourseId = module.CourseId,
                CourseName = module.Course.Name,
                UniversityId = module.Course.UniversityId,
                UniversityName = module.Course.University?.Name,
                OpenAIFileId = fileEntity.OpenAIFileId,
                Status = fileEntity.Status,
                ErrorMessage = fileEntity.ErrorMessage,
                CreatedAt = fileEntity.CreatedAt,
                UpdatedAt = fileEntity.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} for module {ModuleId}", sanitizedName, moduleId);
            return StatusCode(500, new { message = "Failed to upload file" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FileDetailDto>> UpdateFile(int id, [FromBody] UpdateFileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var file = await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        // Access control
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                // Non-admin professors must be assigned to the course
                var hasAccess = await _accessControl.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId,
                    file.Module.CourseId);

                if (!hasAccess)
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin professors can only access files in their university
                if (file.Module.Course.UniversityId != currentUser.UniversityId)
                {
                    return Forbid();
                }
            }
        }

        // Update only allowed fields
        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            file.FileName = FileHelper.SanitizeFilename(request.FileName);
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new FileDetailDto
        {
            Id = file.Id,
            FileName = file.FileName,
            BlobName = file.BlobName,
            ContentType = file.ContentType,
            Size = file.Size,
            ModuleId = file.ModuleId,
            ModuleName = file.Module?.Name,
            OpenAIFileId = file.OpenAIFileId,
            Status = file.Status,
            ErrorMessage = file.ErrorMessage,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFile(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var file = await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        // Access control - non-admin professors can only delete from courses they're assigned to
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                var hasAccess = await _accessControl.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId,
                    file.Module.CourseId);

                if (!hasAccess)
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin professors can only delete files in their university
                if (file.Module.Course.UniversityId != currentUser.UniversityId)
                {
                    return Forbid();
                }
            }
        }

        try
        {
            // Delete from Azure Blob Storage
            await _blobStorageService.DeleteFileAsync(file.BlobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from blob storage: {BlobName}", file.BlobName);
            // Continue with database deletion even if blob deletion fails
        }

        // Delete from database
        _context.Files.Remove(file);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted file {FileName} with ID {Id}", file.FileName, file.Id);

        return Ok(new { message = "File deleted successfully" });
    }

    [HttpGet("{id}/download")]
    public async Task<ActionResult> DownloadFile(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var file = await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        // Access control - non-admin professors can only download from courses they're assigned to
        if (currentUser.UserType == "professor")
        {
            if (!(currentUser.IsAdmin ?? false))
            {
                var hasAccess = await _accessControl.IsProfessorAssignedToCourseAsync(
                    currentUser.UserId,
                    file.Module.CourseId);

                if (!hasAccess)
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin professors can only access files in their university
                if (file.Module.Course.UniversityId != currentUser.UniversityId)
                {
                    return Forbid();
                }
            }
        }

        try
        {
            // Generate download URL with SAS token
            var downloadUrl = _blobStorageService.GetDownloadUrl(file.BlobName, expiresInHours: 1);

            // Redirect to the download URL
            return Redirect(downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for file {FileId}", id);
            return StatusCode(500, new { message = "Failed to generate download URL" });
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

        var file = await _context.Files
            .Include(f => f.Module)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        file.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.ErrorMessage))
        {
            file.ErrorMessage = request.ErrorMessage;
        }
        if (!string.IsNullOrWhiteSpace(request.OpenAIFileId))
        {
            file.OpenAIFileId = request.OpenAIFileId;
        }

        file.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated file status for {FileName} to {Status}", file.FileName, file.Status);

        return Ok(new FileDetailDto
        {
            Id = file.Id,
            FileName = file.FileName,
            BlobName = file.BlobName,
            ContentType = file.ContentType,
            Size = file.Size,
            ModuleId = file.ModuleId,
            ModuleName = file.Module?.Name,
            OpenAIFileId = file.OpenAIFileId,
            Status = file.Status,
            ErrorMessage = file.ErrorMessage,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        });
    }
}
