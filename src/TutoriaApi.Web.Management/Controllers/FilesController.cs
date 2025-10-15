using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Management.DTOs;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Manages file uploads and storage for module content.
/// </summary>
/// <remarks>
/// Handles file operations for module learning materials, including upload to Azure Blob Storage
/// and integration with OpenAI's file API for vector store attachments.
///
/// **Authorization**: All endpoints require ProfessorOrAbove policy.
///
/// **Storage**:
/// - Files are stored in Azure Blob Storage
/// - Database tracks metadata and OpenAI file associations
/// - Files are organized by: university/course/module hierarchy
///
/// **File Statuses**:
/// - `pending`: Uploaded but not yet processed by OpenAI
/// - `processing`: Being processed by OpenAI
/// - `completed`: Successfully processed and attached to vector store
/// - `failed`: Processing failed (see ErrorMessage for details)
/// </remarks>
[ApiController]
[Route("api/files")]
[Authorize(Policy = "ProfessorOrAbove")]
public class FilesController : ControllerBase
{
    private readonly TutoriaDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        TutoriaDbContext context,
        IBlobStorageService blobStorageService,
        ILogger<FilesController> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
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

        var query = _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .AsQueryable();

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
        var file = await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.University)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
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

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFile(int id)
    {
        var file = await _context.Files.FindAsync(id);
        if (file == null)
        {
            return NotFound(new { message = "File not found" });
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

    [HttpPut("{id}/status")]
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

    [HttpPost]
    public async Task<ActionResult<FileDetailDto>> UploadFile(
        [FromForm] int moduleId,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
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

        // Get file extension
        var extension = Path.GetExtension(file.FileName);
        var originalFileName = file.FileName;

        // Generate blob path
        var blobPath = _blobStorageService.GenerateBlobPath(
            module.Course.UniversityId,
            module.CourseId,
            moduleId,
            originalFileName
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
                FileName = originalFileName,
                BlobName = blobPath,
                ContentType = file.ContentType ?? "application/octet-stream",
                Size = file.Length,
                ModuleId = moduleId,
                Status = "pending"
            };

            _context.Files.Add(fileEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded file {FileName} for module {ModuleId}", originalFileName, moduleId);

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
            _logger.LogError(ex, "Failed to upload file {FileName} for module {ModuleId}", originalFileName, moduleId);
            return StatusCode(500, new { message = "Failed to upload file" });
        }
    }

    [HttpGet("{id}/download")]
    public async Task<ActionResult> DownloadFile(int id)
    {
        var file = await _context.Files
            .Include(f => f.Module)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound(new { message = "File not found" });
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
}
