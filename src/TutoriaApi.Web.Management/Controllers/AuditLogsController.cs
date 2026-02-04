using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.Management.DTOs;

namespace TutoriaApi.Web.Management.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditLogService auditLogService,
        ILogger<AuditLogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AnalyticsAccess")]  // SuperAdmin or Manager
    public async Task<ActionResult<PaginatedResponse<AuditLogDto>>> GetAuditLogs(
        [FromQuery] int? universityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            var (items, total) = await _auditLogService.GetPagedAsync(
                universityId, userId, action, entityType, startDate, endDate, search, page, pageSize, currentUser);

            var dtos = items.Select(MapToDto).ToList();

            return Ok(new PaginatedResponse<AuditLogDto>
            {
                Items = dtos,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, new { message = "An error occurred while retrieving audit logs" });
        }
    }

    [HttpGet("export")]
    [Authorize(Policy = "AnalyticsAccess")]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] int? universityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            var csvBytes = await _auditLogService.ExportToCsvAsync(
                universityId, userId, action, entityType, startDate, endDate, currentUser);

            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(csvBytes, "text/csv", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return StatusCode(500, new { message = "An error occurred while exporting audit logs" });
        }
    }

    private User GetCurrentUserFromClaims()
    {
        var userId = int.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value ?? "0");
        var username = User.FindFirst("name")?.Value ?? User.FindFirst("username")?.Value ?? "";
        var userType = User.FindFirst("role")?.Value ?? User.FindFirst("userType")?.Value ?? "";
        var universityIdClaim = User.FindFirst("universityId")?.Value;

        return new User
        {
            UserId = userId,
            Username = username,
            UserType = userType,
            UniversityId = string.IsNullOrEmpty(universityIdClaim) ? null : int.Parse(universityIdClaim)
        };
    }

    private AuditLogDto MapToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            Username = log.Username,
            UniversityId = log.UniversityId,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            EntityName = log.EntityName,
            Changes = log.Changes,
            CreatedAt = log.CreatedAt
        };
    }
}
