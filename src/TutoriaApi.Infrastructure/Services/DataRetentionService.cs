using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Hangfire background job service for LGPD data retention compliance.
/// Automatically cleans up old interaction logs and anonymizes inactive students.
/// </summary>
public class DataRetentionService : IDataRetentionService
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly int _logRetentionDays;
    private readonly int _studentInactivityDays;

    public DataRetentionService(
        TutoriaDbContext context,
        IConfiguration configuration,
        ILogger<DataRetentionService> logger)
    {
        _context = context;
        _logger = logger;

        // Load configuration with defaults
        _logRetentionDays = configuration.GetValue("DataRetention:LogRetentionDays", 365);
        _studentInactivityDays = configuration.GetValue("DataRetention:StudentInactivityDays", 730); // 2 years

        _logger.LogInformation(
            "DataRetentionService initialized: LogRetentionDays={LogDays}, StudentInactivityDays={StudentDays}",
            _logRetentionDays, _studentInactivityDays);
    }

    public async Task<DataRetentionResult> RunCleanupAsync()
    {
        _logger.LogInformation("🧹 [DataRetention] Starting LGPD data retention cleanup job");

        var result = new DataRetentionResult();

        try
        {
            // 1. Delete old interaction logs (Logs table - managed by Python API, no EF entity)
            result.DeletedLogs = await DeleteOldLogsAsync();

            // 2. Anonymize long-inactive students
            result.AnonymizedStudents = await AnonymizeInactiveStudentsAsync();

            // 3. Clean up revoked/expired consents older than retention period
            result.RevokedExpiredConsents = await CleanupOldRevokedConsentsAsync();

            _logger.LogInformation(
                "✅ [DataRetention] Cleanup completed: {DeletedLogs} logs deleted, " +
                "{AnonymizedStudents} students anonymized, {RevokedConsents} old revoked consents removed",
                result.DeletedLogs, result.AnonymizedStudents, result.RevokedExpiredConsents);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DataRetention] Fatal error during cleanup job");
            throw;
        }
    }

    /// <summary>
    /// Delete interaction logs older than the configured retention period.
    /// Uses raw SQL since the Logs table has no EF entity in the .NET API.
    /// </summary>
    private async Task<int> DeleteOldLogsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_logRetentionDays);

            var deletedCount = await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM [dbo].[Logs] WHERE [CreatedAt] < {0}",
                cutoffDate);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "🗑️ [DataRetention] Deleted {Count} interaction logs older than {Days} days (before {CutoffDate:yyyy-MM-dd})",
                    deletedCount, _logRetentionDays, cutoffDate);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DataRetention] Error deleting old logs");
            return 0;
        }
    }

    /// <summary>
    /// Anonymize student records that have been inactive for longer than the retention period.
    /// Does not delete the record (preserves referential integrity), but scrubs PII.
    /// </summary>
    private async Task<int> AnonymizeInactiveStudentsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_studentInactivityDays);

            // Find students who:
            // - Are of type 'student'
            // - Have not logged in since the cutoff (or never logged in and were created before cutoff)
            // - Are not already anonymized
            var inactiveStudents = await _context.Users
                .Where(u => u.UserType == "student"
                         && u.IsActive == true
                         && !u.Email.StartsWith("deleted_")
                         && (
                             (u.LastLoginAt != null && u.LastLoginAt < cutoffDate) ||
                             (u.LastLoginAt == null && u.CreatedAt < cutoffDate)
                         ))
                .ToListAsync();

            if (inactiveStudents.Count == 0)
            {
                return 0;
            }

            foreach (var student in inactiveStudents)
            {
                student.FirstName = "Anonimizado";
                student.LastName = "Anonimizado";
                student.Email = $"deleted_{student.UserId}@anonimizado.tutoria.tec.br";
                student.Username = $"deleted_{student.UserId}";
                student.GovernmentId = null;
                student.ExternalId = null;
                student.Birthdate = null;
                student.IsActive = false;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "👤 [DataRetention] Anonymized {Count} students inactive for more than {Days} days",
                inactiveStudents.Count, _studentInactivityDays);

            return inactiveStudents.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DataRetention] Error anonymizing inactive students");
            return 0;
        }
    }

    /// <summary>
    /// Remove revoked consent records older than retention period (they've served their audit purpose).
    /// </summary>
    private async Task<int> CleanupOldRevokedConsentsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_logRetentionDays);

            var oldRevokedConsents = await _context.Consents
                .Where(c => c.RevokedAt != null && c.RevokedAt < cutoffDate)
                .ToListAsync();

            if (oldRevokedConsents.Count == 0)
            {
                return 0;
            }

            _context.Consents.RemoveRange(oldRevokedConsents);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "📋 [DataRetention] Removed {Count} old revoked consent records",
                oldRevokedConsents.Count);

            return oldRevokedConsents.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DataRetention] Error cleaning up old revoked consents");
            return 0;
        }
    }
}
