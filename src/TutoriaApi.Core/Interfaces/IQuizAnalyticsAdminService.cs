namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// On-demand maintenance of the pre-computed QuizAnalytics table. The actual
/// aggregation (DynamoDB QuizAttempts → SQL) lives in tutoria-worker and runs
/// nightly; this triggers the same job now so staff don't wait for the 3:30 AM run.
/// </summary>
public interface IQuizAnalyticsAdminService
{
    /// <summary>
    /// Ask the worker to rebuild QuizAnalytics from the last <paramref name="windowDays"/>
    /// of attempts (worker default when null). Returns the number of modules rebuilt.
    /// </summary>
    Task<int> RecomputeAsync(int? windowDays = null);
}
