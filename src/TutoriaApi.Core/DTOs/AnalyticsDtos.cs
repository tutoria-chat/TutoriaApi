using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Core.DTOs;

// =====================================
// 1. Cost Analysis DTOs
// =====================================

/// <summary>
/// Comprehensive cost breakdown with hierarchical filtering
/// </summary>
public class CostAnalysisDto
{
    public long TotalMessages { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public Dictionary<string, ProviderCostDto> CostByProvider { get; set; } = new();
    public Dictionary<string, ModelCostDto> CostByModel { get; set; } = new();
    public Dictionary<int, decimal> CostByModule { get; set; } = new();
    public Dictionary<int, decimal> CostByCourse { get; set; } = new();
    public Dictionary<int, decimal> CostByUniversity { get; set; } = new();

    // Video Transcription Costs
    public double TranscriptionCostUSD { get; set; }
    public int TranscriptionVideoCount { get; set; }
    public int TranscriptionTotalDurationSeconds { get; set; }
    public Dictionary<int, decimal> TranscriptionCostByModule { get; set; } = new();
}

public class CostByProviderDto
{
    public string Provider { get; set; } = string.Empty;
    public long MessageCount { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
}

public class CostByModelDto
{
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public long MessageCount { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public decimal InputCostPer1M { get; set; }
    public decimal OutputCostPer1M { get; set; }
}

/// <summary>
/// Today's costs with real-time updates
/// </summary>
public class TodayCostDto
{
    public DateTime Date { get; set; }
    public long TotalMessages { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public Dictionary<string, double> CostByProvider { get; set; } = new();
    public double ProjectedDailyCost { get; set; }
    public CostComparisonDto? ComparedToYesterday { get; set; }

    // Video Transcription Costs
    public double TranscriptionCostUSD { get; set; }
    public int TranscriptionVideoCount { get; set; }
    public double ProjectedDailyTranscriptionCost { get; set; }
}

public class CostComparisonDto
{
    public double PercentageChange { get; set; }
    public double AbsoluteChange { get; set; }
}

// =====================================
// 2. Usage Statistics DTOs
// =====================================

/// <summary>
/// Comprehensive today's usage stats
/// </summary>
public class UsageStatsDto
{
    public DateTime Date { get; set; }
    public long TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public int ActiveModules { get; set; }
    public long TotalTokens { get; set; }
    public double AverageResponseTime { get; set; }
    public double EstimatedCostUSD { get; set; }
    public Dictionary<string, long> MessagesByProvider { get; set; } = new();
    public Dictionary<string, long> MessagesByModel { get; set; } = new();
    public PeakHourDto? PeakHour { get; set; }
}

public class PeakHourDto
{
    public int Hour { get; set; }
    public long MessageCount { get; set; }
}

/// <summary>
/// Usage trends over time (daily aggregation)
/// </summary>
public class UsageTrendsResponseDto
{
    public List<UsageTrendDto> Trends { get; set; } = new();
    public UsageSummaryDto Summary { get; set; } = new();
}

public class UsageTrendDto
{
    public DateTime Date { get; set; }
    public long TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public double AverageResponseTime { get; set; }
}

public class UsageSummaryDto
{
    public long TotalPeriodMessages { get; set; }
    public double TotalPeriodCost { get; set; }
    public double AverageDailyMessages { get; set; }
    public double AverageDailyCost { get; set; }
    public double GrowthRate { get; set; }
    public string TrendDirection { get; set; } = string.Empty; // "increasing", "stable", "decreasing"
}

/// <summary>
/// Hourly usage breakdown for peak time analysis
/// </summary>
public class HourlyUsageResponseDto
{
    public DateTime Date { get; set; }
    public List<HourlyUsageDto> HourlyBreakdown { get; set; } = new();
    public HourlyInsightsDto Insights { get; set; } = new();
}

public class HourlyUsageDto
{
    public int Hour { get; set; }
    public long MessageCount { get; set; }
    public int UniqueStudents { get; set; }
    public int UniqueConversations { get; set; }
    public double AverageResponseTime { get; set; }
}

public class HourlyInsightsDto
{
    public int PeakHour { get; set; }
    public long PeakHourMessages { get; set; }
    public int QuietestHour { get; set; }
    public long QuietestHourMessages { get; set; }
    public long BusinessHoursTotal { get; set; }
    public long AfterHoursTotal { get; set; }
}

// =====================================
// 3. Student & Engagement DTOs
// =====================================

/// <summary>
/// Top active students by message count
/// </summary>
public class TopActiveStudentsResponseDto
{
    public List<TopActiveStudentDto> TopStudents { get; set; } = new();
    public StudentSummaryDto Summary { get; set; } = new();
}

public class TopActiveStudentDto
{
    public int StudentId { get; set; }
    public long TotalMessages { get; set; }
    public int UniqueConversations { get; set; }
    public int UniqueModules { get; set; }
    public DateTime FirstMessageAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public double AverageMessagesPerConversation { get; set; }
}

public class StudentSummaryDto
{
    public int TotalStudentsAnalyzed { get; set; }
    public double AverageMessagesPerStudent { get; set; }
    public double MedianMessagesPerStudent { get; set; }
}

/// <summary>
/// Conversation engagement metrics
/// </summary>
public class ConversationMetricsDto
{
    public int TotalConversations { get; set; }
    public double AverageMessagesPerConversation { get; set; }
    public double MedianMessagesPerConversation { get; set; }
    public int SingleMessageConversations { get; set; }
    public int ShortConversations { get; set; }
    public int MediumConversations { get; set; }
    public int LongConversations { get; set; }
    public double ConversationCompletionRate { get; set; }
    public TimeSpan AverageConversationDuration { get; set; }
    public Dictionary<string, int> ConversationDistribution { get; set; } = new();
    public ConversationInsightsDto Insights { get; set; } = new();
}

public class ConversationInsightsDto
{
    public string EngagementQuality { get; set; } = string.Empty; // "low", "medium", "high"
    public double DropoffRate { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

// =====================================
// 4. Performance & Quality DTOs
// =====================================

/// <summary>
/// Response quality and performance metrics
/// </summary>
public class ResponseQualityDto
{
    public double AverageResponseTime { get; set; }
    public double MedianResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double AverageTokensPerMessage { get; set; }
    public double TokenEfficiencyScore { get; set; }
    public long FastResponses { get; set; }
    public long SlowResponses { get; set; }
    public Dictionary<string, long> ResponseTimeDistribution { get; set; } = new();
    public string PerformanceGrade { get; set; } = string.Empty; // "A", "B", "C", "D", "F"
    public PerformanceInsightsDto Insights { get; set; } = new();
}

public class PerformanceInsightsDto
{
    public string Status { get; set; } = string.Empty; // "healthy", "warning", "critical"
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

// =====================================
// 5. Module Comparison DTOs
// =====================================

/// <summary>
/// Compare multiple modules side-by-side
/// </summary>
public class ModuleComparisonResponseDto
{
    public List<ModuleComparisonDetailDto> Modules { get; set; } = new();
    public ModuleComparisonInsightsDto Insights { get; set; } = new();
}

public class ModuleComparisonDetailDto
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public long TotalMessages { get; set; }
    public int UniqueStudents { get; set; }
    public double AverageMessagesPerStudent { get; set; }
    public double AverageResponseTime { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
    public double EngagementScore { get; set; }
}

public class ModuleComparisonInsightsDto
{
    public TopPerformerDto? MostActiveModule { get; set; }
    public TopPerformerDto? MostEngagedModule { get; set; }
    public TopPerformerDto? MostEfficientModule { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class TopPerformerDto
{
    public int ModuleId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// =====================================
// 6. Executive Dashboard DTOs
// =====================================

/// <summary>
/// High-level summary for executives and super admins
/// </summary>
public class DashboardSummaryDto
{
    public string Period { get; set; } = string.Empty; // "today", "week", "month", "quarter", "year"
    public DateRangeDto DateRange { get; set; } = new();
    public OverviewDto Overview { get; set; } = new();
    public GrowthDto Growth { get; set; } = new();
    public TopPerformersDto TopPerformers { get; set; } = new();
    public CostBreakdownDto CostBreakdown { get; set; } = new();
    public HealthIndicatorsDto HealthIndicators { get; set; } = new();
}

public class DateRangeDto
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public class OverviewDto
{
    public long TotalMessages { get; set; }
    public double TotalCostUSD { get; set; }
    public int UniqueStudents { get; set; }
    public int ActiveModules { get; set; }
    public int ActiveCourses { get; set; }
    public int ActiveUniversities { get; set; }
}

public class GrowthDto
{
    public double MessagesGrowth { get; set; }
    public double StudentGrowth { get; set; }
    public double CostGrowth { get; set; }
}

public class TopPerformersDto
{
    public TopModuleDto? MostActiveModule { get; set; }
    public TopCourseDto? MostEngagedCourse { get; set; }
    public TopUniversityDto? MostActiveUniversity { get; set; }
}

public class TopModuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Messages { get; set; }
}

public class TopCourseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double AvgMessagesPerStudent { get; set; }
}

public class TopUniversityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Messages { get; set; }
}

public class CostBreakdownDto
{
    public Dictionary<string, double> ByProvider { get; set; } = new();
    public TopCostModuleDto? TopCostModule { get; set; }
}

public class TopCostModuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Cost { get; set; }
}

public class HealthIndicatorsDto
{
    public string SystemHealth { get; set; } = string.Empty; // "excellent", "good", "fair", "poor"
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public double Uptime { get; set; }
}

// =====================================
// 7. Frequently Asked Questions DTOs
// =====================================

/// <summary>
/// Most frequently asked questions by students
/// </summary>
public class FrequentlyAskedQuestionsResponseDto
{
    public List<FrequentQuestionDto> Questions { get; set; } = new();
    public FrequentQuestionSummaryDto Summary { get; set; } = new();
}

public class FrequentQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public List<string> SimilarQuestions { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public DateTime FirstAskedAt { get; set; }
    public DateTime LastAskedAt { get; set; }
}

public class FrequentQuestionSummaryDto
{
    public int TotalUniqueQuestions { get; set; }
    public int TotalQuestions { get; set; }
    public double AverageQuestionsPerStudent { get; set; }
    public List<string> TopCategories { get; set; } = new();
}

// =====================================
// 8. Query Filter DTOs (Request Parameters)
// =====================================

/// <summary>
/// Common query filters for analytics endpoints
/// </summary>
public class AnalyticsFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? UniversityId { get; set; }
    public int? CourseId { get; set; }
    public int? ModuleId { get; set; }
}

/// <summary>
/// Top students query parameters
/// </summary>
public class TopStudentsFilterDto : AnalyticsFilterDto
{
    [Range(1, 100)]
    public int Limit { get; set; } = 10;
}

/// <summary>
/// Module comparison query parameters
/// </summary>
public class ModuleComparisonFilterDto
{
    [Required]
    public List<int> ModuleIds { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Dashboard period filter
/// </summary>
public class DashboardFilterDto
{
    public int? UniversityId { get; set; }

    [Required]
    public string Period { get; set; } = "month"; // "today", "week", "month", "quarter", "year"
}

// =====================================
// 9. Legacy/Backward Compatibility DTOs
// (Used by existing DynamoDbAnalyticsService)
// =====================================

/// <summary>
/// Legacy DTO for provider cost (used by DynamoDbAnalyticsService)
/// </summary>
public class ProviderCostDto
{
    public string Provider { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public long TotalTokens { get; set; }
    public double EstimatedCostUSD { get; set; }
}

/// <summary>
/// Legacy DTO for model cost (used by DynamoDbAnalyticsService)
/// </summary>
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
/// Legacy DTO for daily usage stats (used by DynamoDbAnalyticsService)
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
/// Legacy DTO for daily trend (used by DynamoDbAnalyticsService)
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
/// Legacy DTO for student activity summary (used by DynamoDbAnalyticsService)
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
/// Legacy DTO for response quality metrics (used by DynamoDbAnalyticsService)
/// </summary>
public class ResponseQualityMetricsDto
{
    public double AverageResponseTime { get; set; }
    public double MedianResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double AverageTokensPerMessage { get; set; }
    public double TokenEfficiencyScore { get; set; }
    public int FastResponses { get; set; }
    public int SlowResponses { get; set; }
}

/// <summary>
/// Legacy DTO for conversation engagement metrics (used by DynamoDbAnalyticsService)
/// </summary>
public class ConversationEngagementMetricsDto
{
    public int TotalConversations { get; set; }
    public double AverageMessagesPerConversation { get; set; }
    public double MedianMessagesPerConversation { get; set; }
    public int SingleMessageConversations { get; set; }
    public int ShortConversations { get; set; }
    public int MediumConversations { get; set; }
    public int LongConversations { get; set; }
    public double ConversationCompletionRate { get; set; }
    public TimeSpan AverageConversationDuration { get; set; }
}

/// <summary>
/// Legacy DTO for module comparison item (used by DynamoDbAnalyticsService)
/// </summary>
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
    public double EngagementScore { get; set; }
}

/// <summary>
/// Unified dashboard response combining all dashboard data in a single request
/// </summary>
public class UnifiedDashboardResponseDto
{
    public DashboardSummaryDto Summary { get; set; } = null!;
    public UsageTrendsResponseDto Trends { get; set; } = null!;
    public UsageStatsDto TodayUsage { get; set; } = null!;
    public TodayCostDto TodayCost { get; set; } = null!;
}

/// <summary>
/// Legacy DTO for module comparison (used by DynamoDbAnalyticsService)
/// Note: Different from ModuleComparisonResponseDto which uses structured insights
/// </summary>
public class ModuleComparisonDto
{
    public List<ModuleComparisonItemDto> Modules { get; set; } = new();
    public Dictionary<string, object> Insights { get; set; } = new();
}
