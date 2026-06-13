using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository,
        ILogger<AuditLogService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<(List<AuditLog> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        int page,
        int pageSize,
        User currentUser)
    {
        // Access control: Manager can only see their university
        if (currentUser.UserType == UserTypes.Manager && currentUser.UniversityId.HasValue)
        {
            universityId = currentUser.UniversityId.Value;
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only super admins and managers can view audit logs");
        }

        var (items, total) = await _auditLogRepository.SearchAsync(
            universityId, userId, action, entityType, startDate, endDate, search, page, pageSize);

        return (items.ToList(), total);
    }

    public async Task<byte[]> ExportToCsvAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        User currentUser)
    {
        // Access control: same as GetPagedAsync
        if (currentUser.UserType == UserTypes.Manager && currentUser.UniversityId.HasValue)
        {
            universityId = currentUser.UniversityId.Value;
        }
        else if (currentUser.UserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only super admins and managers can export audit logs");
        }

        var logs = await _auditLogRepository.GetAllForExportAsync(
            universityId, userId, action, entityType, startDate, endDate);

        // Build CSV
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,User,Action,Entity Type,Entity,Changes");

        foreach (var log in logs)
        {
            var changes = ParseChangesForDisplay(log.Changes);
            csv.AppendLine($"\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{log.Username}\",\"{log.Action}\",\"{log.EntityType}\",\"{log.EntityName ?? log.EntityId.ToString()}\",\"{EscapeCsv(changes)}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task LogAsync(
        int userId,
        string username,
        int? universityId,
        string action,
        string entityType,
        int entityId,
        string? entityName,
        Dictionary<string, (object? OldValue, object? NewValue)>? changes = null)
    {
        var changesJson = changes != null && changes.Any()
            ? JsonSerializer.Serialize(changes.ToDictionary(
                kvp => kvp.Key,
                kvp => new { Old = kvp.Value.OldValue, New = kvp.Value.NewValue }))
            : null;

        // Username is NOT NULL in the DB. Callers sometimes only have the userId,
        // so resolve it here (and fall back) — a null would otherwise blow up the
        // whole request the audit log was attached to.
        var resolvedUsername = username;
        if (string.IsNullOrWhiteSpace(resolvedUsername))
        {
            try { resolvedUsername = (await _userRepository.GetByIdAsync(userId))?.Username; }
            catch { /* best-effort */ }
        }
        if (string.IsNullOrWhiteSpace(resolvedUsername))
            resolvedUsername = userId > 0 ? $"user#{userId}" : "system";

        var auditLog = new AuditLog
        {
            UserId = userId,
            Username = resolvedUsername,
            UniversityId = universityId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Changes = changesJson
        };

        // Audit logging must never break the operation it records.
        try
        {
            await _auditLogRepository.AddAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log ({Action} {EntityType} {EntityId})",
                action, entityType, entityId);
        }
    }

    private string ParseChangesForDisplay(string? changesJson)
    {
        if (string.IsNullOrWhiteSpace(changesJson)) return "N/A";

        try
        {
            var changes = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(changesJson);
            if (changes == null || !changes.Any()) return "N/A";

            var parts = changes.Select(kvp => $"{kvp.Key}: {kvp.Value["Old"]}→{kvp.Value["New"]}");
            return string.Join("; ", parts);
        }
        catch
        {
            return changesJson;  // Fallback to raw JSON
        }
    }

    private string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
