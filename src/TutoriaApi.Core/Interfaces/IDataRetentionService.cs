namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Service for automatic data retention cleanup (LGPD compliance).
/// Runs as a Hangfire recurring job to delete old interaction logs
/// and anonymize inactive student records.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Runs the data retention cleanup job.
    /// - Deletes interaction logs older than the configured retention period (default: 365 days)
    /// - Anonymizes student records that have been inactive for the retention period
    /// </summary>
    /// <returns>Summary of cleaned-up records</returns>
    Task<DataRetentionResult> RunCleanupAsync();
}

/// <summary>
/// Result of a data retention cleanup run
/// </summary>
public class DataRetentionResult
{
    public int DeletedLogs { get; set; }
    public int AnonymizedStudents { get; set; }
    public int RevokedExpiredConsents { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
}
