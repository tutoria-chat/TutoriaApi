using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Service for querying DynamoDB chat analytics
/// </summary>
public interface IDynamoDbAnalyticsService
{
    /// <summary>
    /// Get conversation history for a specific conversation ID
    /// </summary>
    Task<List<ChatMessageDto>> GetConversationHistoryAsync(string conversationId, int limit = 50);

    /// <summary>
    /// Get all chat messages for a specific module with optional date filtering
    /// </summary>
    Task<List<ChatMessageDto>> GetModuleAnalyticsAsync(
        int moduleId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000);

    /// <summary>
    /// Get all chat messages for a specific student with optional date filtering
    /// </summary>
    Task<List<ChatMessageDto>> GetStudentActivityAsync(
        int studentId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100);

    /// <summary>
    /// Get usage statistics for a specific AI provider with optional date filtering
    /// </summary>
    Task<List<ChatMessageDto>> GetProviderUsageAsync(
        string provider,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000);

    /// <summary>
    /// Get aggregated statistics for a module (total messages, unique students, etc.)
    /// </summary>
    Task<ModuleAnalyticsSummaryDto> GetModuleSummaryAsync(
        int moduleId,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Generate FAQ from most common questions for a module
    /// </summary>
    Task<List<FaqItemDto>> GenerateFaqFromQuestionsAsync(int moduleId, int minimumOccurrences = 3, int maxResults = 10);

    /// <summary>
    /// Get detailed cost analysis with filtering by university, course, module
    /// </summary>
    Task<CostAnalysisDto> GetCostAnalysisAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? universityId = null,
        int? courseId = null,
        int? moduleId = null);

    /// <summary>
    /// Get usage statistics for today with optional filtering
    /// </summary>
    Task<DailyUsageStatsDto> GetTodayUsageStatsAsync(
        int? universityId = null,
        int? courseId = null,
        int? moduleId = null);

    /// <summary>
    /// Get hourly usage breakdown for peak time analysis
    /// </summary>
    Task<List<HourlyUsageDto>> GetHourlyUsageBreakdownAsync(
        DateTime? date = null,
        int? universityId = null,
        int? courseId = null,
        int? moduleId = null);

    /// <summary>
    /// Get usage trends over time (daily aggregation)
    /// </summary>
    Task<List<DailyTrendDto>> GetUsageTrendsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? universityId = null,
        int? courseId = null,
        int? moduleId = null);

    /// <summary>
    /// Get top active students by message count
    /// </summary>
    Task<List<StudentActivitySummaryDto>> GetTopActiveStudentsAsync(
        int limit = 10,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? moduleId = null);

    /// <summary>
    /// Get average response quality metrics (response time, token efficiency)
    /// </summary>
    Task<ResponseQualityMetricsDto> GetResponseQualityMetricsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? moduleId = null);

    /// <summary>
    /// Get conversation engagement metrics (avg messages per conversation, completion rate)
    /// </summary>
    Task<ConversationEngagementMetricsDto> GetConversationEngagementMetricsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? moduleId = null);

    /// <summary>
    /// Get module comparison analytics (compare multiple modules)
    /// </summary>
    Task<ModuleComparisonDto> GetModuleComparisonAsync(
        List<int> moduleIds,
        DateTime? startDate = null,
        DateTime? endDate = null);
}

/// <summary>
/// DTO for chat messages from DynamoDB
/// </summary>
public class ChatMessageDto
{
    public string ConversationId { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int ModuleId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool HasFile { get; set; }
    public string? FileName { get; set; }
    public int? TokenCount { get; set; }
    public int? ResponseTime { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// DTO for module analytics summary
/// </summary>
public class ModuleAnalyticsSummaryDto
{
    public int ModuleId { get; set; }
    public int TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public double AverageResponseTime { get; set; }
    public int TotalTokensUsed { get; set; }
    public Dictionary<string, int> ModelUsage { get; set; } = new();
    public Dictionary<string, int> ProviderUsage { get; set; } = new();
}

/// <summary>
/// DTO for FAQ generation
/// </summary>
public class FaqItemDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public double SimilarityScore { get; set; }
}
