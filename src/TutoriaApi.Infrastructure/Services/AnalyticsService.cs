using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// High-level analytics service with role-based access control and SQL enrichment
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IDynamoDbAnalyticsService _dynamoDbService;
    private readonly IModuleRepository _moduleRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IAIModelRepository _aiModelRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<AnalyticsService> _logger;

    // Token distribution constants for cost estimation (based on typical chat usage patterns)
    // Most chat interactions have longer outputs (AI responses) than inputs (user questions)
    private const double INPUT_TOKEN_RATIO = 0.25;  // 25% of tokens are input (user questions)
    private const double OUTPUT_TOKEN_RATIO = 0.75; // 75% of tokens are output (AI responses)

    // Compiled regex for FAQ quiz answer filtering (performance optimization)
    private static readonly System.Text.RegularExpressions.Regex QuizAnswerPattern =
        new System.Text.RegularExpressions.Regex(
            @"^(LETRA\s)?[A-E][\)\.]*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public AnalyticsService(
        IDynamoDbAnalyticsService dynamoDbService,
        IModuleRepository moduleRepository,
        ICourseRepository courseRepository,
        IUniversityRepository universityRepository,
        IAIModelRepository aiModelRepository,
        IFileRepository fileRepository,
        ILogger<AnalyticsService> logger)
    {
        _dynamoDbService = dynamoDbService;
        _moduleRepository = moduleRepository;
        _courseRepository = courseRepository;
        _universityRepository = universityRepository;
        _aiModelRepository = aiModelRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    #region Cost Analysis

    public async Task<CostAnalysisDto> GetCostAnalysisAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            // Get authorized module IDs
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            if (!moduleIds.Any())
            {
                return new CostAnalysisDto();
            }

            // Get all messages for authorized modules - parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            // TODO: Long-term: Implement GetMultipleModulesAnalyticsAsync() for true batch querying
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            // Get AI models for accurate pricing
            Dictionary<string, TutoriaApi.Core.Entities.AIModel> aiModels;
            if (allMessages.Any())
            {
                aiModels = (await _aiModelRepository.GetActiveModelsAsync()).ToDictionary(m => m.ModelName, m => m);
            }
            else
            {
                aiModels = new Dictionary<string, TutoriaApi.Core.Entities.AIModel>();
            }

            // Calculate costs by provider
            var costByProvider = allMessages
                .GroupBy(m => m.Provider)
                .ToDictionary(
                    g => g.Key.ToLower(),
                    g => new ProviderCostDto
                    {
                        Provider = g.Key,
                        MessageCount = g.Count(),
                        TotalTokens = g.Sum(m => m.TokenCount ?? 0),
                        EstimatedCostUSD = CalculateProviderCost(g.ToList(), aiModels)
                    });

            // Calculate costs by model
            var costByModel = allMessages
                .GroupBy(m => m.ModelUsed)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var aiModel = aiModels.TryGetValue(g.Key, out var m) ? m : null;
                        return new ModelCostDto
                        {
                            Model = g.Key,
                            Provider = g.FirstOrDefault()?.Provider ?? "",
                            MessageCount = g.Count(),
                            TotalTokens = g.Sum(msg => msg.TokenCount ?? 0),
                            EstimatedCostUSD = CalculateModelCost(g.Key, g.Sum(msg => msg.TokenCount ?? 0), aiModels),
                            InputCostPer1M = aiModel != null ? (double)(aiModel.InputCostPer1M ?? 0) : 0,
                            OutputCostPer1M = aiModel != null ? (double)(aiModel.OutputCostPer1M ?? 0) : 0
                        };
                    });

            // Calculate costs by module
            var costByModule = allMessages
                .GroupBy(m => m.ModuleId)
                .ToDictionary(
                    g => g.Key,
                    g => (decimal)CalculateProviderCost(g.ToList(), aiModels));

            // Calculate costs by course (requires module → course mapping)
            var moduleIdsToCourseIds = await GetModuleToCourseMapping(moduleIds);
            var costByCourse = allMessages
                .GroupBy(m => moduleIdsToCourseIds.TryGetValue(m.ModuleId, out var courseId) ? courseId : 0)
                .Where(g => g.Key != 0)
                .ToDictionary(
                    g => g.Key,
                    g => (decimal)CalculateProviderCost(g.ToList(), aiModels));

            // Calculate costs by university (requires module → university mapping)
            var moduleIdsToUniversityIds = await GetModuleToUniversityMapping(moduleIds);
            var costByUniversity = allMessages
                .GroupBy(m => moduleIdsToUniversityIds.TryGetValue(m.ModuleId, out var universityId) ? universityId : 0)
                .Where(g => g.Key != 0)
                .ToDictionary(
                    g => g.Key,
                    g => (decimal)CalculateProviderCost(g.ToList(), aiModels));

            // Get video transcription costs
            var transcriptionCosts = await GetTranscriptionCostsAsync(moduleIds, filters.StartDate, filters.EndDate);

            return new CostAnalysisDto
            {
                TotalMessages = allMessages.Count,
                TotalTokens = allMessages.Sum(m => m.TokenCount ?? 0),
                EstimatedCostUSD = costByProvider.Values.Sum(v => v.EstimatedCostUSD),
                CostByProvider = costByProvider,
                CostByModel = costByModel,
                CostByModule = costByModule,
                CostByCourse = costByCourse,
                CostByUniversity = costByUniversity,
                TranscriptionCostUSD = (double)transcriptionCosts.TotalCostUSD,
                TranscriptionVideoCount = transcriptionCosts.VideoCount,
                TranscriptionTotalDurationSeconds = transcriptionCosts.TotalDurationSeconds,
                TranscriptionCostByModule = transcriptionCosts.CostByModule
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost analysis for user {UserId}", userId);
            return new CostAnalysisDto();
        }
    }

    public async Task<TodayCostDto> GetTodayCostAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            filters.StartDate = today;
            filters.EndDate = tomorrow;

            var costAnalysis = await GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);

            // Get yesterday's costs for comparison
            var yesterday = today.AddDays(-1);
            var yesterdayFilters = new AnalyticsFilterDto
            {
                StartDate = yesterday,
                EndDate = today,
                UniversityId = filters.UniversityId,
                CourseId = filters.CourseId,
                ModuleId = filters.ModuleId
            };
            var yesterdayCostAnalysis = await GetCostAnalysisAsync(userId, userRole, userUniversityId, yesterdayFilters);

            // Calculate projected daily cost based on current hour
            // Only project if we have at least 3 hours of data to avoid unrealistic early-day estimates
            var currentHour = DateTime.UtcNow.Hour;
            const int MinimumHoursForProjection = 3;

            var projectedDailyCost = currentHour >= MinimumHoursForProjection
                ? (costAnalysis.EstimatedCostUSD / currentHour) * 24
                : costAnalysis.EstimatedCostUSD;

            // Calculate projected daily transcription cost
            var projectedDailyTranscriptionCost = currentHour >= MinimumHoursForProjection
                ? (costAnalysis.TranscriptionCostUSD / currentHour) * 24
                : costAnalysis.TranscriptionCostUSD;

            // Calculate comparison
            CostComparisonDto? comparison = null;
            if (yesterdayCostAnalysis.EstimatedCostUSD > 0)
            {
                var absoluteChange = costAnalysis.EstimatedCostUSD - yesterdayCostAnalysis.EstimatedCostUSD;
                var percentageChange = (absoluteChange / yesterdayCostAnalysis.EstimatedCostUSD) * 100;

                comparison = new CostComparisonDto
                {
                    PercentageChange = percentageChange,
                    AbsoluteChange = absoluteChange
                };
            }

            return new TodayCostDto
            {
                Date = today,
                TotalMessages = costAnalysis.TotalMessages,
                TotalTokens = costAnalysis.TotalTokens,
                EstimatedCostUSD = costAnalysis.EstimatedCostUSD,
                CostByProvider = costAnalysis.CostByProvider.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.EstimatedCostUSD),
                ProjectedDailyCost = projectedDailyCost,
                ComparedToYesterday = comparison,
                TranscriptionCostUSD = costAnalysis.TranscriptionCostUSD,
                TranscriptionVideoCount = costAnalysis.TranscriptionVideoCount,
                ProjectedDailyTranscriptionCost = projectedDailyTranscriptionCost
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's cost for user {UserId}", userId);
            return new TodayCostDto { Date = DateTime.UtcNow.Date };
        }
    }

    #endregion

    #region Usage Statistics

    public async Task<UsageStatsDto> GetTodayUsageStatsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            filters.StartDate = today;
            filters.EndDate = tomorrow;

            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, today, tomorrow, limit: 10000));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var aiModels = (await _aiModelRepository.GetActiveModelsAsync()).ToDictionary(m => m.ModelName, m => m);

            var messagesByProvider = allMessages.GroupBy(m => m.Provider).ToDictionary(g => g.Key, g => (long)g.Count());
            var messagesByModel = allMessages.GroupBy(m => m.ModelUsed).ToDictionary(g => g.Key, g => (long)g.Count());

            // Find peak hour
            var hourlyBreakdown = allMessages
                .GroupBy(m => DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).Hour)
                .Select(g => new { Hour = g.Key, Count = (long)g.Count() })
                .OrderByDescending(h => h.Count)
                .FirstOrDefault();

            return new UsageStatsDto
            {
                Date = today,
                TotalMessages = allMessages.Count,
                UniqueStudents = allMessages.Select(m => m.StudentId).Distinct().Count(),
                UniqueConversations = allMessages.Select(m => m.ConversationId).Distinct().Count(),
                ActiveModules = allMessages.Select(m => m.ModuleId).Distinct().Count(),
                TotalTokens = allMessages.Sum(m => m.TokenCount ?? 0),
                AverageResponseTime = allMessages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average(),
                EstimatedCostUSD = CalculateProviderCost(allMessages, aiModels),
                MessagesByProvider = messagesByProvider,
                MessagesByModel = messagesByModel,
                PeakHour = hourlyBreakdown != null ? new PeakHourDto { Hour = hourlyBreakdown.Hour, MessageCount = hourlyBreakdown.Count } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's usage stats for user {UserId}", userId);
            return new UsageStatsDto { Date = DateTime.UtcNow.Date };
        }
    }

    public async Task<UsageTrendsResponseDto> GetUsageTrendsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var aiModels = (await _aiModelRepository.GetActiveModelsAsync()).ToDictionary(m => m.ModelName, m => m);

            var trends = allMessages
                .GroupBy(m => DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).Date)
                .Select(g => new UsageTrendDto
                {
                    Date = g.Key,
                    TotalMessages = g.Count(),
                    UniqueStudents = g.Select(m => m.StudentId).Distinct().Count(),
                    UniqueConversations = g.Select(m => m.ConversationId).Distinct().Count(),
                    TotalTokens = g.Sum(m => m.TokenCount ?? 0),
                    EstimatedCostUSD = CalculateProviderCost(g.ToList(), aiModels),
                    AverageResponseTime = g.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average()
                })
                .OrderBy(t => t.Date)
                .ToList();

            var summary = new UsageSummaryDto
            {
                TotalPeriodMessages = trends.Sum(t => t.TotalMessages),
                TotalPeriodCost = trends.Sum(t => t.EstimatedCostUSD),
                AverageDailyMessages = trends.Any() ? trends.Average(t => t.TotalMessages) : 0,
                AverageDailyCost = trends.Any() ? trends.Average(t => t.EstimatedCostUSD) : 0,
                GrowthRate = CalculateGrowthRate(trends),
                TrendDirection = DetermineTrendDirection(trends)
            };

            return new UsageTrendsResponseDto
            {
                Trends = trends,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage trends for user {UserId}", userId);
            return new UsageTrendsResponseDto();
        }
    }

    public async Task<HourlyUsageResponseDto> GetHourlyUsageAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        DateTime? date,
        AnalyticsFilterDto filters)
    {
        try
        {
            var targetDate = (date ?? DateTime.UtcNow).Date;
            var nextDay = targetDate.AddDays(1);

            filters.StartDate = targetDate;
            filters.EndDate = nextDay;

            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, targetDate, nextDay, limit: 10000));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var hourlyBreakdown = allMessages
                .GroupBy(m => DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).Hour)
                .Select(g => new HourlyUsageDto
                {
                    Hour = g.Key,
                    MessageCount = g.Count(),
                    UniqueStudents = g.Select(m => m.StudentId).Distinct().Count(),
                    UniqueConversations = g.Select(m => m.ConversationId).Distinct().Count(),
                    AverageResponseTime = g.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average()
                })
                .OrderBy(h => h.Hour)
                .ToList();

            var insights = new HourlyInsightsDto
            {
                PeakHour = hourlyBreakdown.OrderByDescending(h => h.MessageCount).FirstOrDefault()?.Hour ?? 0,
                PeakHourMessages = hourlyBreakdown.OrderByDescending(h => h.MessageCount).FirstOrDefault()?.MessageCount ?? 0,
                QuietestHour = hourlyBreakdown.OrderBy(h => h.MessageCount).FirstOrDefault()?.Hour ?? 0,
                QuietestHourMessages = hourlyBreakdown.OrderBy(h => h.MessageCount).FirstOrDefault()?.MessageCount ?? 0,
                BusinessHoursTotal = hourlyBreakdown.Where(h => h.Hour >= 8 && h.Hour <= 18).Sum(h => h.MessageCount),
                AfterHoursTotal = hourlyBreakdown.Where(h => h.Hour < 8 || h.Hour > 18).Sum(h => h.MessageCount)
            };

            return new HourlyUsageResponseDto
            {
                Date = targetDate,
                HourlyBreakdown = hourlyBreakdown,
                Insights = insights
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hourly usage for user {UserId}", userId);
            return new HourlyUsageResponseDto { Date = date ?? DateTime.UtcNow.Date };
        }
    }

    #endregion

    #region Student & Engagement

    public async Task<TopActiveStudentsResponseDto> GetTopActiveStudentsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        TopStudentsFilterDto filters)
    {
        try
        {
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var topStudents = allMessages
                .Where(m => m.StudentId > 0)
                .GroupBy(m => m.StudentId)
                .Select(g => new TopActiveStudentDto
                {
                    StudentId = g.Key,
                    TotalMessages = g.Count(),
                    UniqueConversations = g.Select(m => m.ConversationId).Distinct().Count(),
                    UniqueModules = g.Select(m => m.ModuleId).Distinct().Count(),
                    FirstMessageAt = DateTimeOffset.FromUnixTimeMilliseconds(g.Min(m => m.Timestamp)).DateTime,
                    LastMessageAt = DateTimeOffset.FromUnixTimeMilliseconds(g.Max(m => m.Timestamp)).DateTime,
                    AverageMessagesPerConversation = g.GroupBy(m => m.ConversationId).Average(c => c.Count())
                })
                .OrderByDescending(s => s.TotalMessages)
                .Take(filters.Limit)
                .ToList();

            var summary = new StudentSummaryDto
            {
                TotalStudentsAnalyzed = allMessages.Where(m => m.StudentId > 0).Select(m => m.StudentId).Distinct().Count(),
                AverageMessagesPerStudent = topStudents.Any() ? topStudents.Average(s => s.TotalMessages) : 0,
                MedianMessagesPerStudent = CalculateMedian(topStudents.Select(s => (double)s.TotalMessages).ToList())
            };

            return new TopActiveStudentsResponseDto
            {
                TopStudents = topStudents,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top active students for user {UserId}", userId);
            return new TopActiveStudentsResponseDto();
        }
    }

    public async Task<ConversationMetricsDto> GetConversationEngagementMetricsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var conversationGroups = allMessages.GroupBy(m => m.ConversationId).ToList();
            var conversationLengths = conversationGroups.Select(g => g.Count()).OrderBy(c => c).ToList();

            var singleMessage = conversationLengths.Count(c => c == 1);
            var shortConv = conversationLengths.Count(c => c >= 2 && c <= 5);
            var mediumConv = conversationLengths.Count(c => c >= 6 && c <= 15);
            var longConv = conversationLengths.Count(c => c >= 16);

            var completionRate = conversationLengths.Count > 0
                ? (conversationLengths.Count(c => c >= 3) * 100.0 / conversationLengths.Count)
                : 0;

            var avgDuration = conversationGroups.Any()
                ? TimeSpan.FromMilliseconds(conversationGroups.Average(g => g.Max(m => m.Timestamp) - g.Min(m => m.Timestamp)))
                : TimeSpan.Zero;

            var engagementQuality = completionRate >= 80 ? "high" : completionRate >= 50 ? "medium" : "low";

            return new ConversationMetricsDto
            {
                TotalConversations = conversationGroups.Count,
                AverageMessagesPerConversation = conversationLengths.Any() ? conversationLengths.Average() : 0,
                MedianMessagesPerConversation = CalculateMedian(conversationLengths.Select(c => (double)c).ToList()),
                SingleMessageConversations = singleMessage,
                ShortConversations = shortConv,
                MediumConversations = mediumConv,
                LongConversations = longConv,
                ConversationCompletionRate = completionRate,
                AverageConversationDuration = avgDuration,
                ConversationDistribution = new Dictionary<string, int>
                {
                    { "1-message", singleMessage },
                    { "2-5 messages", shortConv },
                    { "6-15 messages", mediumConv },
                    { "16+ messages", longConv }
                },
                Insights = new ConversationInsightsDto
                {
                    EngagementQuality = engagementQuality,
                    DropoffRate = 100 - completionRate,
                    RecommendedActions = GenerateConversationRecommendations(singleMessage, conversationGroups.Count, completionRate)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation metrics for user {UserId}", userId);
            return new ConversationMetricsDto();
        }
    }

    #endregion

    #region Performance & Quality

    public async Task<ResponseQualityDto> GetResponseQualityMetricsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var responseTimes = allMessages
                .Where(m => m.ResponseTime.HasValue)
                .Select(m => m.ResponseTime!.Value)
                .OrderBy(t => t)
                .ToList();

            var avgResponseTime = responseTimes.Any() ? responseTimes.Average() : 0;
            var medianResponseTime = CalculateMedian(responseTimes.Select(t => (double)t).ToList());
            var p95ResponseTime = responseTimes.Any() ? responseTimes[(int)(responseTimes.Count * 0.95)] : 0;
            var p99ResponseTime = responseTimes.Any() ? responseTimes[(int)(responseTimes.Count * 0.99)] : 0;

            var avgTokens = allMessages.Where(m => m.TokenCount.HasValue).Select(m => m.TokenCount!.Value).DefaultIfEmpty(0).Average();
            var tokenEfficiency = avgResponseTime > 0 ? avgTokens / avgResponseTime : 0;

            var fastResponses = responseTimes.Count(t => t < 2000);
            var slowResponses = responseTimes.Count(t => t > 10000);

            var performanceGrade = avgResponseTime < 2000 ? "A" :
                                   avgResponseTime < 3000 ? "B" :
                                   avgResponseTime < 5000 ? "C" :
                                   avgResponseTime < 10000 ? "D" : "F";

            return new ResponseQualityDto
            {
                AverageResponseTime = avgResponseTime,
                MedianResponseTime = medianResponseTime,
                P95ResponseTime = p95ResponseTime,
                P99ResponseTime = p99ResponseTime,
                AverageTokensPerMessage = avgTokens,
                TokenEfficiencyScore = tokenEfficiency,
                FastResponses = fastResponses,
                SlowResponses = slowResponses,
                ResponseTimeDistribution = new Dictionary<string, long>
                {
                    { "< 1s", responseTimes.Count(t => t < 1000) },
                    { "1-2s", responseTimes.Count(t => t >= 1000 && t < 2000) },
                    { "2-5s", responseTimes.Count(t => t >= 2000 && t < 5000) },
                    { "5-10s", responseTimes.Count(t => t >= 5000 && t < 10000) },
                    { "10s+", responseTimes.Count(t => t >= 10000) }
                },
                PerformanceGrade = performanceGrade,
                Insights = new PerformanceInsightsDto
                {
                    Status = performanceGrade == "A" || performanceGrade == "B" ? "healthy" :
                             performanceGrade == "C" ? "warning" : "critical",
                    Issues = GeneratePerformanceIssues(slowResponses, responseTimes.Count),
                    Recommendations = GeneratePerformanceRecommendations(avgResponseTime, slowResponses)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response quality metrics for user {UserId}", userId);
            return new ResponseQualityDto();
        }
    }

    #endregion

    #region Module Comparison

    public async Task<ModuleComparisonResponseDto> GetModuleComparisonAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        ModuleComparisonFilterDto filters)
    {
        try
        {
            // Verify user has access to all requested modules
            var authorizedModuleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, new AnalyticsFilterDto());
            var requestedModuleIds = filters.ModuleIds.Where(id => authorizedModuleIds.Contains(id)).ToList();

            if (!requestedModuleIds.Any())
            {
                return new ModuleComparisonResponseDto();
            }

            var aiModels = (await _aiModelRepository.GetActiveModelsAsync()).ToDictionary(m => m.ModelName, m => m);
            var modules = new List<ModuleComparisonDetailDto>();

            foreach (var moduleId in requestedModuleIds)
            {
                // TODO: Add pagination support to handle high-volume modules (10K+ messages)
                var messages = await _dynamoDbService.GetModuleAnalyticsAsync(
                    moduleId,
                    filters.StartDate,
                    filters.EndDate);

                var module = await _moduleRepository.GetByIdAsync(moduleId);
                var uniqueStudents = messages.Select(m => m.StudentId).Distinct().Count();

                modules.Add(new ModuleComparisonDetailDto
                {
                    ModuleId = moduleId,
                    ModuleName = module?.Name ?? $"Module {moduleId}",
                    TotalMessages = messages.Count,
                    UniqueStudents = uniqueStudents,
                    AverageMessagesPerStudent = uniqueStudents > 0 ? (double)messages.Count / uniqueStudents : 0,
                    AverageResponseTime = messages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average(),
                    TotalTokens = messages.Sum(m => m.TokenCount ?? 0),
                    EstimatedCostUSD = CalculateProviderCost(messages, aiModels),
                    EngagementScore = uniqueStudents > 0 ? (double)messages.Count / uniqueStudents : 0
                });
            }

            var mostActive = modules.OrderByDescending(m => m.TotalMessages).FirstOrDefault();
            var mostEngaged = modules.OrderByDescending(m => m.EngagementScore).FirstOrDefault();
            var mostEfficient = modules.OrderBy(m => m.TotalMessages > 0 ? m.EstimatedCostUSD / m.TotalMessages : double.MaxValue).FirstOrDefault();

            return new ModuleComparisonResponseDto
            {
                Modules = modules,
                Insights = new ModuleComparisonInsightsDto
                {
                    MostActiveModule = mostActive != null ? new TopPerformerDto { ModuleId = mostActive.ModuleId, Reason = "Highest message count" } : null,
                    MostEngagedModule = mostEngaged != null ? new TopPerformerDto { ModuleId = mostEngaged.ModuleId, Reason = "Highest messages per student" } : null,
                    MostEfficientModule = mostEfficient != null ? new TopPerformerDto { ModuleId = mostEfficient.ModuleId, Reason = "Lowest cost per message" } : null,
                    Recommendations = GenerateModuleComparisonRecommendations(modules)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module comparison for user {UserId}", userId);
            return new ModuleComparisonResponseDto();
        }
    }

    #endregion

    #region Dashboard

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        DashboardFilterDto filters)
    {
        try
        {
            var (startDate, endDate) = GetDateRangeFromPeriod(filters.Period);

            var analyticsFilters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                UniversityId = filters.UniversityId
            };

            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, analyticsFilters);

            // Parallel queries to avoid N+1 problem
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, startDate, endDate, limit: 10000));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            var aiModels = (await _aiModelRepository.GetActiveModelsAsync()).ToDictionary(m => m.ModelName, m => m);

            // Get previous period for growth calculation
            var periodLength = endDate - startDate;
            var previousStart = startDate - periodLength;
            var previousEnd = startDate;

            // Parallel queries for previous period to avoid N+1 problem
            var previousMessageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, previousStart, previousEnd, limit: 10000));
            var previousMessageResults = await Task.WhenAll(previousMessageTasks);
            var previousMessages = previousMessageResults.SelectMany(messages => messages).ToList();

            // Calculate growth rates
            var messageGrowth = CalculateGrowthPercentage(previousMessages.Count, allMessages.Count);
            var studentGrowth = CalculateGrowthPercentage(
                previousMessages.Select(m => m.StudentId).Distinct().Count(),
                allMessages.Select(m => m.StudentId).Distinct().Count());
            var costGrowth = CalculateGrowthPercentage(
                CalculateProviderCost(previousMessages, aiModels),
                CalculateProviderCost(allMessages, aiModels));

            // Get module/course/university metadata
            var moduleIdsInUse = allMessages.Select(m => m.ModuleId).Distinct().ToList();
            var moduleToCourse = await GetModuleToCourseMapping(moduleIdsInUse);
            var moduleToUniversity = await GetModuleToUniversityMapping(moduleIdsInUse);

            var activeCourses = moduleToCourse.Values.Distinct().Count();
            var activeUniversities = moduleToUniversity.Values.Distinct().Count();

            // Find top performers
            var moduleStats = allMessages
                .GroupBy(m => m.ModuleId)
                .Select(g => new
                {
                    ModuleId = g.Key,
                    Messages = g.Count(),
                    UniqueStudents = g.Select(m => m.StudentId).Distinct().Count()
                })
                .ToList();

            var topModule = moduleStats.OrderByDescending(m => m.Messages).FirstOrDefault();
            var topModuleEntity = topModule != null ? await _moduleRepository.GetByIdAsync(topModule.ModuleId) : null;

            return new DashboardSummaryDto
            {
                Period = filters.Period,
                DateRange = new DateRangeDto { Start = startDate, End = endDate },
                Overview = new OverviewDto
                {
                    TotalMessages = allMessages.Count,
                    TotalCostUSD = CalculateProviderCost(allMessages, aiModels),
                    UniqueStudents = allMessages.Select(m => m.StudentId).Distinct().Count(),
                    ActiveModules = moduleIdsInUse.Count,
                    ActiveCourses = activeCourses,
                    ActiveUniversities = activeUniversities
                },
                Growth = new GrowthDto
                {
                    MessagesGrowth = messageGrowth,
                    StudentGrowth = studentGrowth,
                    CostGrowth = costGrowth
                },
                TopPerformers = new TopPerformersDto
                {
                    MostActiveModule = topModule != null ? new TopModuleDto
                    {
                        Id = topModule.ModuleId,
                        Name = topModuleEntity?.Name ?? $"Module {topModule.ModuleId}",
                        Messages = topModule.Messages
                    } : null
                },
                CostBreakdown = new CostBreakdownDto
                {
                    ByProvider = allMessages.GroupBy(m => m.Provider).ToDictionary(
                        g => g.Key.ToLower(),
                        g => CalculateProviderCost(g.ToList(), aiModels))
                },
                HealthIndicators = new HealthIndicatorsDto
                {
                    SystemHealth = "excellent",
                    AverageResponseTime = allMessages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average(),
                    ErrorRate = 0.0, // TODO: Implement error tracking
                    Uptime = 99.98 // TODO: Implement uptime tracking
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard summary for user {UserId}", userId);
            return new DashboardSummaryDto { Period = filters.Period };
        }
    }

    #endregion

    #region Frequently Asked Questions

    public async Task<FrequentlyAskedQuestionsResponseDto> GetFrequentlyAskedQuestionsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        try
        {
            var moduleIds = await GetAuthorizedModuleIdsAsync(userId, userRole, userUniversityId, filters);

            // Parallel queries to avoid N+1 problem
            // TODO: Add pagination support to handle high-volume modules (10K+ messages)
            var messageTasks = moduleIds.Select(moduleId =>
                _dynamoDbService.GetModuleAnalyticsAsync(moduleId, filters.StartDate, filters.EndDate));
            var messageResults = await Task.WhenAll(messageTasks);
            var allMessages = messageResults.SelectMany(messages => messages).ToList();

            // Group similar questions using fuzzy matching
            var questionGroups = GroupSimilarQuestionsFuzzy(allMessages, similarityThreshold: 75, minOccurrences: 1)
                .OrderByDescending(g => g.Count)
                .Take(20)
                .Select(g => new FrequentQuestionDto
                {
                    Question = g.RepresentativeQuestion,
                    Count = g.Count,
                    Percentage = (double)g.Count / allMessages.Count * 100,
                    SimilarQuestions = g.SimilarVariations.Take(5).ToList(),
                    Category = CategorizeQuestion(g.RepresentativeQuestion),
                    FirstAskedAt = DateTimeOffset.FromUnixTimeMilliseconds(g.FirstTimestamp).DateTime,
                    LastAskedAt = DateTimeOffset.FromUnixTimeMilliseconds(g.LastTimestamp).DateTime
                })
                .ToList();

            var topCategories = questionGroups
                .GroupBy(q => q.Category)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            return new FrequentlyAskedQuestionsResponseDto
            {
                Questions = questionGroups,
                Summary = new FrequentQuestionSummaryDto
                {
                    TotalUniqueQuestions = questionGroups.Count,
                    TotalQuestions = allMessages.Count,
                    AverageQuestionsPerStudent = allMessages.Select(m => m.StudentId).Distinct().Count() > 0
                        ? (double)allMessages.Count / allMessages.Select(m => m.StudentId).Distinct().Count()
                        : 0,
                    TopCategories = topCategories
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting frequently asked questions for user {UserId}", userId);
            return new FrequentlyAskedQuestionsResponseDto();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get authorized module IDs based on user role and filters
    /// </summary>
    private async Task<List<int>> GetAuthorizedModuleIdsAsync(
        int userId,
        string userRole,
        int? userUniversityId,
        AnalyticsFilterDto filters)
    {
        // If specific module is requested, verify access and return just that module
        if (filters.ModuleId.HasValue)
        {
            var module = await _moduleRepository.GetByIdAsync(filters.ModuleId.Value);
            if (module == null) return new List<int>();

            var hasAccess = await VerifyModuleAccess(userId, userRole, userUniversityId, module);
            return hasAccess ? new List<int> { module.Id } : new List<int>();
        }

        // Get modules based on role
        if (userRole.ToLower() == "super_admin")
        {
            // Super admin sees all modules (with optional filtering)
            var modules = await _moduleRepository.GetAllAsync();

            if (filters.UniversityId.HasValue)
            {
                var courseIds = (await _courseRepository.GetByUniversityIdAsync(filters.UniversityId.Value))
                    .Select(c => c.Id)
                    .ToList();
                modules = modules.Where(m => courseIds.Contains(m.CourseId)).ToList();
            }

            if (filters.CourseId.HasValue)
            {
                modules = modules.Where(m => m.CourseId == filters.CourseId.Value).ToList();
            }

            return modules.Select(m => m.Id).ToList();
        }
        else if (userRole.ToLower() == "professor" && userUniversityId.HasValue)
        {
            // Professor sees only their university's modules
            var courses = await _courseRepository.GetByUniversityIdAsync(userUniversityId.Value);
            var courseIds = courses.Select(c => c.Id).ToList();

            if (filters.CourseId.HasValue)
            {
                if (!courseIds.Contains(filters.CourseId.Value))
                {
                    return new List<int>(); // Requested course not in their university
                }
                courseIds = new List<int> { filters.CourseId.Value };
            }

            var modules = await _moduleRepository.GetAllAsync();
            return modules.Where(m => courseIds.Contains(m.CourseId)).Select(m => m.Id).ToList();
        }

        return new List<int>();
    }

    private async Task<bool> VerifyModuleAccess(int userId, string userRole, int? userUniversityId, TutoriaApi.Core.Entities.Module module)
    {
        if (userRole.ToLower() == "super_admin")
        {
            return true;
        }

        if (userRole.ToLower() == "professor" && userUniversityId.HasValue)
        {
            var course = await _courseRepository.GetByIdAsync(module.CourseId);
            return course?.UniversityId == userUniversityId;
        }

        return false;
    }

    private async Task<Dictionary<int, int>> GetModuleToCourseMapping(List<int> moduleIds)
    {
        var modules = await _moduleRepository.GetAllAsync();
        return modules
            .Where(m => moduleIds.Contains(m.Id))
            .ToDictionary(m => m.Id, m => m.CourseId);
    }

    private async Task<Dictionary<int, int>> GetModuleToUniversityMapping(List<int> moduleIds)
    {
        var modules = await _moduleRepository.GetAllAsync();
        var courseIds = modules.Where(m => moduleIds.Contains(m.Id)).Select(m => m.CourseId).Distinct().ToList();
        var courses = await _courseRepository.GetAllAsync();

        var coursesToUniversities = courses
            .Where(c => courseIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.UniversityId);

        return modules
            .Where(m => moduleIds.Contains(m.Id))
            .ToDictionary(
                m => m.Id,
                m => coursesToUniversities.TryGetValue(m.CourseId, out var univId) ? univId : 0);
    }

    private double CalculateProviderCost(List<ChatMessageDto> messages, Dictionary<string, TutoriaApi.Core.Entities.AIModel> aiModels)
    {
        double totalCost = 0;

        foreach (var message in messages)
        {
            if (message.TokenCount.HasValue && aiModels.TryGetValue(message.ModelUsed, out var model))
            {
                var inputTokens = (long)(message.TokenCount.Value * INPUT_TOKEN_RATIO);
                var outputTokens = (long)(message.TokenCount.Value * OUTPUT_TOKEN_RATIO);

                var inputCost = (inputTokens / 1_000_000.0) * (double)(model.InputCostPer1M ?? 0);
                var outputCost = (outputTokens / 1_000_000.0) * (double)(model.OutputCostPer1M ?? 0);

                totalCost += inputCost + outputCost;
            }
        }

        return totalCost;
    }

    private double CalculateModelCost(string modelName, long totalTokens, Dictionary<string, TutoriaApi.Core.Entities.AIModel> aiModels)
    {
        if (aiModels.TryGetValue(modelName, out var model))
        {
            var inputTokens = (long)(totalTokens * INPUT_TOKEN_RATIO);
            var outputTokens = (long)(totalTokens * OUTPUT_TOKEN_RATIO);

            var inputCost = (inputTokens / 1_000_000.0) * (double)(model.InputCostPer1M ?? 0);
            var outputCost = (outputTokens / 1_000_000.0) * (double)(model.OutputCostPer1M ?? 0);

            return inputCost + outputCost;
        }

        return 0;
    }

    private double CalculateMedian(List<double> values)
    {
        if (!values.Any()) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private double CalculateGrowthRate(List<UsageTrendDto> trends)
    {
        if (trends.Count < 2) return 0;

        var first = trends.First().TotalMessages;
        var last = trends.Last().TotalMessages;

        return first > 0 ? ((last - first) / (double)first) * 100 : 0;
    }

    private string DetermineTrendDirection(List<UsageTrendDto> trends)
    {
        if (trends.Count < 2) return "stable";

        var growthRate = CalculateGrowthRate(trends);

        return growthRate > 10 ? "increasing" :
               growthRate < -10 ? "decreasing" : "stable";
    }

    private (DateTime startDate, DateTime endDate) GetDateRangeFromPeriod(string period)
    {
        var now = DateTime.UtcNow;

        return period.ToLower() switch
        {
            "today" => (now.Date, now.Date.AddDays(1)),
            "week" => (now.AddDays(-7), now),
            "month" => (now.AddMonths(-1), now),
            "quarter" => (now.AddMonths(-3), now),
            "year" => (now.AddYears(-1), now),
            _ => (now.AddMonths(-1), now) // Default to month
        };
    }

    private double CalculateGrowthPercentage(int previous, int current)
    {
        return previous > 0 ? ((current - previous) / (double)previous) * 100 : 0;
    }

    private double CalculateGrowthPercentage(double previous, double current)
    {
        return previous > 0 ? ((current - previous) / previous) * 100 : 0;
    }

    private List<string> GenerateConversationRecommendations(int singleMessage, int total, double completionRate)
    {
        var recommendations = new List<string>();

        var singleMessageRate = total > 0 ? (double)singleMessage / total * 100 : 0;

        if (singleMessageRate > 30)
        {
            recommendations.Add("Focus on reducing single-message conversations");
        }

        if (completionRate < 50)
        {
            recommendations.Add("Improve engagement to increase conversation completion rate");
        }
        else
        {
            recommendations.Add("Average conversation length is healthy");
        }

        return recommendations;
    }

    private List<string> GeneratePerformanceIssues(long slowResponses, int total)
    {
        var issues = new List<string>();

        var slowRate = total > 0 ? (double)slowResponses / total * 100 : 0;

        if (slowRate > 2)
        {
            issues.Add($"{slowRate:F1}% of responses exceed 10 seconds");
        }

        return issues;
    }

    private List<string> GeneratePerformanceRecommendations(double avgResponseTime, long slowResponses)
    {
        var recommendations = new List<string>();

        if (avgResponseTime > 5000)
        {
            recommendations.Add("Consider caching for common questions");
            recommendations.Add("Review prompt optimization for faster responses");
        }

        if (slowResponses > 10)
        {
            recommendations.Add("Investigate slow response patterns");
        }

        return recommendations;
    }

    private List<string> GenerateModuleComparisonRecommendations(List<ModuleComparisonDetailDto> modules)
    {
        var recommendations = new List<string>();

        var mostEngaged = modules.OrderByDescending(m => m.EngagementScore).FirstOrDefault();
        var leastEngaged = modules.OrderBy(m => m.EngagementScore).FirstOrDefault();

        if (mostEngaged != null && leastEngaged != null && mostEngaged.EngagementScore > leastEngaged.EngagementScore * 2)
        {
            recommendations.Add($"{mostEngaged.ModuleName} shows high engagement - analyze best practices");
        }

        var highCostModule = modules.OrderByDescending(m => m.TotalMessages > 0 ? m.EstimatedCostUSD / m.TotalMessages : 0).FirstOrDefault();
        if (highCostModule != null && highCostModule.TotalMessages > 0)
        {
            var costPerMessage = highCostModule.EstimatedCostUSD / highCostModule.TotalMessages;
            if (costPerMessage > 0.01) // More than 1 cent per message
            {
                recommendations.Add($"{highCostModule.ModuleName} has higher costs - review prompt optimization");
            }
        }

        return recommendations;
    }

    private string NormalizeQuestion(string question)
    {
        return question
            .ToLowerInvariant()
            .Trim()
            .TrimEnd('?', '.', '!')
            .Replace("  ", " ");
    }

    /// <summary>
    /// Group similar questions using fuzzy string matching (Levenshtein distance)
    /// </summary>
    private List<QuestionGroup> GroupSimilarQuestionsFuzzy(
        List<ChatMessageDto> messages,
        int similarityThreshold = 75,
        int minOccurrences = 1)
    {
        var groups = new List<QuestionGroup>();
        var processedMessages = new HashSet<ChatMessageDto>();

        // Filter out quiz answers first
        var validMessages = messages
            .Where(m => !IsQuizAnswer(m.Question))
            .ToList();

        foreach (var message in validMessages)
        {
            if (processedMessages.Contains(message))
                continue;

            // Create a new group
            var group = new QuestionGroup
            {
                RepresentativeQuestion = message.Question,
                Count = 1,
                SimilarVariations = new List<string> { message.Question },
                FirstTimestamp = message.Timestamp,
                LastTimestamp = message.Timestamp
            };

            processedMessages.Add(message);

            // Find similar questions
            foreach (var otherMessage in validMessages)
            {
                if (processedMessages.Contains(otherMessage))
                    continue;

                var similarity = FuzzySharp.Fuzz.Ratio(
                    NormalizeQuestion(message.Question),
                    NormalizeQuestion(otherMessage.Question));

                if (similarity >= similarityThreshold)
                {
                    group.Count++;
                    if (group.SimilarVariations.Count < 10)
                        group.SimilarVariations.Add(otherMessage.Question);

                    group.FirstTimestamp = Math.Min(group.FirstTimestamp, otherMessage.Timestamp);
                    group.LastTimestamp = Math.Max(group.LastTimestamp, otherMessage.Timestamp);

                    processedMessages.Add(otherMessage);
                }
            }

            // Only include groups meeting minimum occurrences
            if (group.Count >= minOccurrences)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    private static bool IsQuizAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        var trimmed = question.Trim().ToUpperInvariant();

        // Filter out single letters (A, B, C, D, E, etc.) which are quiz answers
        if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
            return true;

        // Filter out single letters with punctuation (A., B., etc.)
        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == '.')
            return true;

        // Filter out patterns like "LETRA A", "LETRA B", "A)", "B)", etc.
        if (QuizAnswerPattern.IsMatch(trimmed))
            return true;

        return false;
    }

    private string CategorizeQuestion(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();

        if (lowerQuestion.Contains("how") || lowerQuestion.Contains("como"))
            return "How-To";
        if (lowerQuestion.Contains("what") || lowerQuestion.Contains("que é") || lowerQuestion.Contains("qué es"))
            return "Definition";
        if (lowerQuestion.Contains("why") || lowerQuestion.Contains("por que") || lowerQuestion.Contains("por qué"))
            return "Explanation";
        if (lowerQuestion.Contains("when") || lowerQuestion.Contains("quando") || lowerQuestion.Contains("cuándo"))
            return "Timing";
        if (lowerQuestion.Contains("example") || lowerQuestion.Contains("exemplo") || lowerQuestion.Contains("ejemplo"))
            return "Example";

        return "General";
    }

    #endregion

    /// <summary>
    /// Get video transcription costs for authorized modules within date range
    /// </summary>
    private async Task<TranscriptionCostsDto> GetTranscriptionCostsAsync(
        List<int> moduleIds,
        DateTime? startDate,
        DateTime? endDate)
    {
        try
        {
            var transcriptions = await _fileRepository.GetCompletedTranscriptionsAsync(
                moduleIds,
                startDate,
                endDate);

            var totalCost = transcriptions.Sum(f => f.TranscriptionCostUSD ?? 0);
            var costByModule = transcriptions
                .GroupBy(f => f.ModuleId)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.TranscriptionCostUSD ?? 0));

            return new TranscriptionCostsDto
            {
                TotalCostUSD = (decimal)totalCost,
                VideoCount = transcriptions.Count,
                TotalDurationSeconds = transcriptions.Sum(f => f.VideoDurationSeconds ?? 0),
                CostByModule = costByModule
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating transcription costs");
            return new TranscriptionCostsDto();
        }
    }

    #region Helper Classes

    /// <summary>
    /// Represents a group of similar questions for FAQ generation
    /// </summary>
    private class QuestionGroup
    {
        public string RepresentativeQuestion { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<string> SimilarVariations { get; set; } = new();
        public long FirstTimestamp { get; set; }
        public long LastTimestamp { get; set; }
    }

    /// <summary>
    /// DTO for video transcription costs
    /// </summary>
    private class TranscriptionCostsDto
    {
        public decimal TotalCostUSD { get; set; }
        public int VideoCount { get; set; }
        public int TotalDurationSeconds { get; set; }
        public Dictionary<int, decimal> CostByModule { get; set; } = new();
    }

    #endregion
}
