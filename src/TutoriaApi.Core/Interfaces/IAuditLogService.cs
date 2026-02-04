using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IAuditLogService
{
    Task<(List<AuditLog> Items, int Total)> GetPagedAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        int page,
        int pageSize,
        User currentUser);

    Task<byte[]> ExportToCsvAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        User currentUser);

    Task LogAsync(
        int userId,
        string username,
        int? universityId,
        string action,
        string entityType,
        int entityId,
        string? entityName,
        Dictionary<string, (object? OldValue, object? NewValue)>? changes = null);
}
