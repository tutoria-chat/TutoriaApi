using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(IEnumerable<AuditLog> Items, int Total)> SearchAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        int page,
        int pageSize);

    Task<IEnumerable<AuditLog>> GetAllForExportAsync(
        int? universityId,
        int? userId,
        string? action,
        string? entityType,
        DateTime? startDate,
        DateTime? endDate);
}
