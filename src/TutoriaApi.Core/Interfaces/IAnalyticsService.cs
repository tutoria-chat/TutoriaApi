using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// High-level analytics service with role-based access control and SQL enrichment
/// </summary>
public interface IAnalyticsService
{
    // =====================================
    // Cost Analysis
    // =====================================

    /// <summary>
    /// Get comprehensive cost breakdown with role-based filtering
    /// </summary>
    Task<CostAnalysisDto> GetCostAnalysisAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    /// <summary>
    /// Get today's costs with real-time updates
    /// </summary>
    Task<TodayCostDto> GetTodayCostAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    // =====================================
    // Usage Statistics
    // =====================================

    /// <summary>
    /// Get comprehensive today's usage stats
    /// </summary>
    Task<UsageStatsDto> GetTodayUsageStatsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    /// <summary>
    /// Get usage trends over time (daily aggregation)
    /// </summary>
    Task<UsageTrendsResponseDto> GetUsageTrendsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    /// <summary>
    /// Get hourly usage breakdown for peak time analysis
    /// </summary>
    Task<HourlyUsageResponseDto> GetHourlyUsageAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        DateTime? date,
        AnalyticsFilterDto filters);

    // =====================================
    // Student & Engagement
    // =====================================

    /// <summary>
    /// Get top active students by message count
    /// </summary>
    Task<TopActiveStudentsResponseDto> GetTopActiveStudentsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        TopStudentsFilterDto filters);

    /// <summary>
    /// Get conversation engagement metrics
    /// </summary>
    Task<ConversationMetricsDto> GetConversationEngagementMetricsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    // =====================================
    // Performance & Quality
    // =====================================

    /// <summary>
    /// Get response quality and performance metrics
    /// </summary>
    Task<ResponseQualityDto> GetResponseQualityMetricsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    // =====================================
    // Module Comparison
    // =====================================

    /// <summary>
    /// Compare multiple modules side-by-side
    /// </summary>
    Task<ModuleComparisonResponseDto> GetModuleComparisonAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        ModuleComparisonFilterDto filters);

    // =====================================
    // Dashboard
    // =====================================

    /// <summary>
    /// Get high-level executive dashboard summary
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        DashboardFilterDto filters);

    // =====================================
    // Frequently Asked Questions
    // =====================================

    /// <summary>
    /// Get most frequently asked questions by students
    /// </summary>
    Task<FrequentlyAskedQuestionsResponseDto> GetFrequentlyAskedQuestionsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters);

    // =====================================
    // Pre-computed Analytics (from SQL)
    // =====================================

    /// <summary>
    /// Get question counts per module from pre-computed daily summaries
    /// </summary>
    Task<QuestionsPerModuleDto> GetQuestionsPerModuleAsync(
        int userId, string userRole, int? userUniversityId, AnalyticsFilterDto filters);

    /// <summary>
    /// Get most demanded topics from pre-computed topic classifications
    /// </summary>
    Task<TopTopicsResponseDto> GetTopTopicsAsync(
        int userId, string userRole, int? userUniversityId, AnalyticsFilterDto filters);

    /// <summary>
    /// Get quiz performance metrics from pre-computed quiz analytics
    /// </summary>
    Task<QuizPerformanceResponseDto> GetQuizPerformanceAsync(
        int userId, string userRole, int? userUniversityId, int? moduleId);
}
