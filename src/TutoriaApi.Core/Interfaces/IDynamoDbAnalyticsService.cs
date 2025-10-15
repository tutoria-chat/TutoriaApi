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

/// <summary>
/// DTO for comprehensive cost analysis
/// </summary>
public class CostAnalysisDto
{
    public int TotalMessages { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public Dictionary<string, ProviderCostDto> CostByProvider { get; set; } = new();
    public Dictionary<string, ModelCostDto> CostByModel { get; set; } = new();
    public Dictionary<int, decimal> CostByModule { get; set; } = new();
    public Dictionary<int, decimal> CostByCourse { get; set; } = new();
    public Dictionary<int, decimal> CostByUniversity { get; set; } = new();
}

public class ProviderCostDto
{
    public string Provider { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
}

public class ModelCostDto
{
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public double InputCostPer1M { get; set; }
    public double OutputCostPer1M { get; set; }
}

/// <summary>
/// DTO for today's usage statistics
/// </summary>
public class DailyUsageStatsDto
{
    public DateTime Date { get; set; }
    public int TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public int ActiveModules { get; set; }
    public long TotalTokens { get; set; }
    public double AverageResponseTime { get; set; }
    public double EstimatedCostUSD { get; set; }
    public Dictionary<string, int> MessagesByProvider { get; set; } = new();
    public Dictionary<string, int> MessagesByModel { get; set; } = new();
}

/// <summary>
/// DTO for hourly usage breakdown
/// </summary>
public class HourlyUsageDto
{
    public int Hour { get; set; } // 0-23
    public int MessageCount { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public double AverageResponseTime { get; set; }
}

/// <summary>
/// DTO for daily usage trends
/// </summary>
public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public int TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public double AverageResponseTime { get; set; }
}

/// <summary>
/// DTO for student activity summary
/// </summary>
public class StudentActivitySummaryDto
{
    public int StudentId { get; set; }
    public int TotalMessages { get; set; }
    public int UniqueConversations { get; set; }
    public int UniqueModules { get; set; }
    public DateTime FirstMessageAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public double AverageMessagesPerConversation { get; set; }
}

/// <summary>
/// DTO for response quality metrics
/// </summary>
public class ResponseQualityMetricsDto
{
    public double AverageResponseTime { get; set; }
    public double MedianResponseTime { get; set; }
    public double P95ResponseTime { get; set; } // 95th percentile
    public double P99ResponseTime { get; set; } // 99th percentile
    public double AverageTokensPerMessage { get; set; }
    public double TokenEfficiencyScore { get; set; } // Tokens per second
    public int FastResponses { get; set; } // < 2 seconds
    public int SlowResponses { get; set; } // > 10 seconds
}

/// <summary>
/// DTO for conversation engagement metrics
/// </summary>
public class ConversationEngagementMetricsDto
{
    public int TotalConversations { get; set; }
    public double AverageMessagesPerConversation { get; set; }
    public double MedianMessagesPerConversation { get; set; }
    public int SingleMessageConversations { get; set; }
    public int ShortConversations { get; set; } // 2-5 messages
    public int MediumConversations { get; set; } // 6-15 messages
    public int LongConversations { get; set; } // 16+ messages
    public double ConversationCompletionRate { get; set; } // % with 3+ messages
    public TimeSpan AverageConversationDuration { get; set; }
}

/// <summary>
/// DTO for module comparison
/// </summary>
public class ModuleComparisonDto
{
    public List<ModuleComparisonItemDto> Modules { get; set; } = new();
    public Dictionary<string, object> Insights { get; set; } = new(); // Key insights
}

public class ModuleComparisonItemDto
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public double AverageMessagesPerStudent { get; set; }
    public double AverageResponseTime { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public double EngagementScore { get; set; } // Calculated metric
}
