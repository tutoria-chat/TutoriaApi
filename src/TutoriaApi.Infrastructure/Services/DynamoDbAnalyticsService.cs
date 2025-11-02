using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Infrastructure.Services;

public class DynamoDbAnalyticsService : IDynamoDbAnalyticsService
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly ILogger<DynamoDbAnalyticsService> _logger;
    private readonly string _tableName;
    private readonly bool _isEnabled;

    public DynamoDbAnalyticsService(
        IConfiguration configuration,
        ILogger<DynamoDbAnalyticsService> logger)
    {
        _logger = logger;
        _tableName = configuration["AWS:DynamoDb:ChatTable"] ?? "ChatMessages";
        _isEnabled = bool.Parse(configuration["AWS:DynamoDb:Enabled"] ?? "false");

        if (_isEnabled)
        {
            var awsRegion = configuration["AWS:Region"] ?? "sa-east-1";
            var awsAccessKey = configuration["AWS:AccessKeyId"];
            var awsSecretKey = configuration["AWS:SecretAccessKey"];

            if (!string.IsNullOrEmpty(awsAccessKey) && !string.IsNullOrEmpty(awsSecretKey))
            {
                _dynamoDbClient = new AmazonDynamoDBClient(
                    awsAccessKey,
                    awsSecretKey,
                    Amazon.RegionEndpoint.GetBySystemName(awsRegion));
                _logger.LogInformation("DynamoDB analytics service initialized for table: {TableName}", _tableName);
            }
            else
            {
                _logger.LogWarning("AWS credentials not configured. DynamoDB analytics disabled.");
                _isEnabled = false;
                _dynamoDbClient = null!;
            }
        }
        else
        {
            _logger.LogInformation("DynamoDB analytics is disabled in configuration");
            _dynamoDbClient = null!;
        }
    }

    public async Task<List<ChatMessageDto>> GetConversationHistoryAsync(string conversationId, int limit = 50)
    {
        if (!_isEnabled || _dynamoDbClient == null)
        {
            _logger.LogWarning("DynamoDB not enabled, returning empty conversation history");
            return new List<ChatMessageDto>();
        }

        try
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "conversationId = :convId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":convId", new AttributeValue { S = conversationId } }
                },
                ScanIndexForward = false, // Descending order (newest first)
                Limit = limit
            };

            var response = await _dynamoDbClient.QueryAsync(request);
            return response.Items.Select(MapToChatMessageDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for {ConversationId}", conversationId);
            return new List<ChatMessageDto>();
        }
    }

    public async Task<List<ChatMessageDto>> GetModuleAnalyticsAsync(
        int moduleId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000)
    {
        if (!_isEnabled || _dynamoDbClient == null)
        {
            _logger.LogWarning("DynamoDB not enabled, returning empty module analytics");
            return new List<ChatMessageDto>();
        }

        try
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "ModuleAnalyticsIndex",
                KeyConditionExpression = "moduleId = :modId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":modId", new AttributeValue { N = moduleId.ToString() } }
                },
                ScanIndexForward = false, // Descending order (newest first)
                Limit = limit
            };

            // Add date filtering if provided
            if (startDate.HasValue && endDate.HasValue)
            {
                var startTimestamp = new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds();
                var endTimestamp = new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds();

                request.KeyConditionExpression += " AND #ts BETWEEN :start AND :end";
                request.ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ts", "timestamp" }
                };
                request.ExpressionAttributeValues.Add(":start", new AttributeValue { N = startTimestamp.ToString() });
                request.ExpressionAttributeValues.Add(":end", new AttributeValue { N = endTimestamp.ToString() });
            }

            var response = await _dynamoDbClient.QueryAsync(request);
            return response.Items.Select(MapToChatMessageDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module analytics for module {ModuleId}", moduleId);
            return new List<ChatMessageDto>();
        }
    }

    public async Task<List<ChatMessageDto>> GetStudentActivityAsync(
        int studentId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100)
    {
        if (!_isEnabled || _dynamoDbClient == null)
        {
            _logger.LogWarning("DynamoDB not enabled, returning empty student activity");
            return new List<ChatMessageDto>();
        }

        try
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "StudentActivityIndex",
                KeyConditionExpression = "studentId = :studId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":studId", new AttributeValue { N = studentId.ToString() } }
                },
                ScanIndexForward = false, // Descending order (newest first)
                Limit = limit
            };

            // Add date filtering if provided
            if (startDate.HasValue && endDate.HasValue)
            {
                var startTimestamp = new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds();
                var endTimestamp = new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds();

                request.KeyConditionExpression += " AND #ts BETWEEN :start AND :end";
                request.ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ts", "timestamp" }
                };
                request.ExpressionAttributeValues.Add(":start", new AttributeValue { N = startTimestamp.ToString() });
                request.ExpressionAttributeValues.Add(":end", new AttributeValue { N = endTimestamp.ToString() });
            }

            var response = await _dynamoDbClient.QueryAsync(request);
            return response.Items.Select(MapToChatMessageDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student activity for student {StudentId}", studentId);
            return new List<ChatMessageDto>();
        }
    }

    public async Task<List<ChatMessageDto>> GetProviderUsageAsync(
        string provider,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000)
    {
        if (!_isEnabled || _dynamoDbClient == null)
        {
            _logger.LogWarning("DynamoDB not enabled, returning empty provider usage");
            return new List<ChatMessageDto>();
        }

        try
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "ProviderUsageIndex",
                KeyConditionExpression = "provider = :prov",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":prov", new AttributeValue { S = provider } }
                },
                ScanIndexForward = false, // Descending order (newest first)
                Limit = limit
            };

            // Add date filtering if provided
            if (startDate.HasValue && endDate.HasValue)
            {
                var startTimestamp = new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds();
                var endTimestamp = new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds();

                request.KeyConditionExpression += " AND #ts BETWEEN :start AND :end";
                request.ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ts", "timestamp" }
                };
                request.ExpressionAttributeValues.Add(":start", new AttributeValue { N = startTimestamp.ToString() });
                request.ExpressionAttributeValues.Add(":end", new AttributeValue { N = endTimestamp.ToString() });
            }

            var response = await _dynamoDbClient.QueryAsync(request);
            return response.Items.Select(MapToChatMessageDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider usage for {Provider}", provider);
            return new List<ChatMessageDto>();
        }
    }

    public async Task<ModuleAnalyticsSummaryDto> GetModuleSummaryAsync(
        int moduleId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var messages = await GetModuleAnalyticsAsync(moduleId, startDate, endDate);

        if (!messages.Any())
        {
            return new ModuleAnalyticsSummaryDto
            {
                ModuleId = moduleId,
                TotalMessages = 0,
                UniqueStudents = 0,
                UniqueConversations = 0,
                AverageResponseTime = 0,
                TotalTokensUsed = 0,
                ModelUsage = new Dictionary<string, int>(),
                ProviderUsage = new Dictionary<string, int>()
            };
        }

        return new ModuleAnalyticsSummaryDto
        {
            ModuleId = moduleId,
            TotalMessages = messages.Count,
            UniqueStudents = messages.Select(m => m.StudentId).Distinct().Count(),
            UniqueConversations = messages.Select(m => m.ConversationId).Distinct().Count(),
            AverageResponseTime = messages
                .Where(m => m.ResponseTime.HasValue)
                .Select(m => m.ResponseTime!.Value)
                .DefaultIfEmpty(0)
                .Average(),
            TotalTokensUsed = messages
                .Where(m => m.TokenCount.HasValue)
                .Sum(m => m.TokenCount!.Value),
            ModelUsage = messages
                .GroupBy(m => m.ModelUsed)
                .ToDictionary(g => g.Key, g => g.Count()),
            ProviderUsage = messages
                .GroupBy(m => m.Provider)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<List<FaqItemDto>> GenerateFaqFromQuestionsAsync(int moduleId, int minimumOccurrences = 1, int maxResults = 10)
    {
        var messages = await GetModuleAnalyticsAsync(moduleId, limit: 5000);

        if (!messages.Any())
        {
            return new List<FaqItemDto>();
        }

        // Group similar questions using fuzzy matching
        var groups = GroupSimilarQuestions(messages, similarityThreshold: 75, minOccurrences: minimumOccurrences);

        var questionGroups = groups
            .OrderByDescending(g => g.Count)
            .Take(maxResults)
            .Select(g => new FaqItemDto
            {
                Question = g.RepresentativeQuestion,
                Answer = g.RepresentativeAnswer,
                Occurrences = g.Count,
                SimilarityScore = 1.0 // All questions in group have >= 75% similarity
            })
            .ToList();

        return questionGroups;
    }

    /// <summary>
    /// Group similar questions using fuzzy string matching
    /// </summary>
    private List<QuestionGroupDto> GroupSimilarQuestions(
        List<ChatMessageDto> messages,
        int similarityThreshold = 75,
        int minOccurrences = 1)
    {
        var groups = new List<QuestionGroupDto>();
        var processedIndices = new HashSet<int>();

        // Filter out quiz answers first
        var validMessages = messages
            .Where(m => !IsQuizAnswer(m.Question))
            .ToList();

        for (int i = 0; i < validMessages.Count; i++)
        {
            if (processedIndices.Contains(i))
                continue;

            var message = validMessages[i];

            // Create a new group
            var group = new QuestionGroupDto
            {
                RepresentativeQuestion = message.Question,
                RepresentativeAnswer = message.Response,
                Count = 1
            };

            processedIndices.Add(i);

            // Find similar questions
            for (int j = i + 1; j < validMessages.Count; j++)
            {
                if (processedIndices.Contains(j))
                    continue;

                var otherMessage = validMessages[j];

                var similarity = FuzzySharp.Fuzz.Ratio(
                    NormalizeQuestion(message.Question),
                    NormalizeQuestion(otherMessage.Question));

                if (similarity >= similarityThreshold)
                {
                    group.Count++;
                    processedIndices.Add(j);

                    // Use the longer answer as representative (usually more detailed)
                    if (otherMessage.Response.Length > group.RepresentativeAnswer.Length)
                    {
                        group.RepresentativeAnswer = otherMessage.Response;
                    }
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

    private class QuestionGroupDto
    {
        public string RepresentativeQuestion { get; set; } = string.Empty;
        public string RepresentativeAnswer { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private static string NormalizeQuestion(string question)
    {
        // Basic normalization - lowercase, trim, remove punctuation
        return question
            .ToLowerInvariant()
            .Trim()
            .TrimEnd('?', '.', '!')
            .Replace("  ", " ");
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
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(LETRA\s)?[A-E][\)\.]*$"))
            return true;

        return false;
    }

    #region New Analytics Methods

    public async Task<CostAnalysisDto> GetCostAnalysisAsync(DateTime? startDate = null, DateTime? endDate = null, int? universityId = null, int? courseId = null, int? moduleId = null)
    {
        var messages = await GetFilteredMessagesAsync(startDate, endDate, universityId, courseId, moduleId);

        if (!messages.Any())
        {
            return new CostAnalysisDto
            {
                TotalMessages = 0,
                TotalTokens = 0,
                EstimatedCostUSD = 0,
                CostByProvider = new Dictionary<string, ProviderCostDto>(),
                CostByModel = new Dictionary<string, ModelCostDto>()
            };
        }

        // Calculate costs (approximate rates)
        var costByProvider = messages
            .GroupBy(m => m.Provider)
            .ToDictionary(
                g => g.Key,
                g => new ProviderCostDto
                {
                    Provider = g.Key,
                    MessageCount = g.Count(),
                    TotalTokens = g.Sum(m => m.TokenCount ?? 0),
                    EstimatedCostUSD = (double)CalculateProviderCost(g.Key, g.Sum(m => m.TokenCount ?? 0))
                });

        var costByModel = messages
            .GroupBy(m => m.ModelUsed)
            .ToDictionary(
                g => g.Key,
                g => new ModelCostDto
                {
                    Model = g.Key,
                    Provider = g.FirstOrDefault()?.Provider ?? "",
                    MessageCount = g.Count(),
                    TotalTokens = g.Sum(m => m.TokenCount ?? 0),
                    EstimatedCostUSD = (double)CalculateModelCost(g.Key, g.Sum(m => m.TokenCount ?? 0)),
                    InputCostPer1M = 0, // TODO: Add actual pricing
                    OutputCostPer1M = 0 // TODO: Add actual pricing
                });

        return new CostAnalysisDto
        {
            TotalMessages = messages.Count,
            TotalTokens = messages.Sum(m => m.TokenCount ?? 0),
            EstimatedCostUSD = costByProvider.Values.Sum(v => v.EstimatedCostUSD),
            CostByProvider = costByProvider,
            CostByModel = costByModel,
            CostByModule = new Dictionary<int, decimal>(), // TODO: Requires SQL join
            CostByCourse = new Dictionary<int, decimal>(), // TODO: Requires SQL join
            CostByUniversity = new Dictionary<int, decimal>() // TODO: Requires SQL join
        };
    }

    public async Task<DailyUsageStatsDto> GetTodayUsageStatsAsync(int? universityId = null, int? courseId = null, int? moduleId = null)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var messages = await GetFilteredMessagesAsync(today, tomorrow, universityId, courseId, moduleId);

        return new DailyUsageStatsDto
        {
            Date = today,
            TotalMessages = messages.Count,
            UniqueStudents = messages.Select(m => m.StudentId).Distinct().Count(),
            UniqueConversations = messages.Select(m => m.ConversationId).Distinct().Count(),
            ActiveModules = messages.Select(m => m.ModuleId).Distinct().Count(),
            TotalTokens = messages.Sum(m => m.TokenCount ?? 0),
            AverageResponseTime = messages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average(),
            EstimatedCostUSD = (double)messages.GroupBy(m => m.Provider).Sum(g => CalculateProviderCost(g.Key, g.Sum(m => m.TokenCount ?? 0))),
            MessagesByProvider = messages.GroupBy(m => m.Provider).ToDictionary(g => g.Key, g => g.Count()),
            MessagesByModel = messages.GroupBy(m => m.ModelUsed).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<List<HourlyUsageDto>> GetHourlyUsageBreakdownAsync(DateTime? date = null, int? universityId = null, int? courseId = null, int? moduleId = null)
    {
        var targetDate = (date ?? DateTime.UtcNow).Date;
        var nextDay = targetDate.AddDays(1);

        var messages = await GetFilteredMessagesAsync(targetDate, nextDay, universityId, courseId, moduleId);

        return messages
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
    }

    public async Task<List<DailyTrendDto>> GetUsageTrendsAsync(DateTime? startDate = null, DateTime? endDate = null, int? universityId = null, int? courseId = null, int? moduleId = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var messages = await GetFilteredMessagesAsync(start, end, universityId, courseId, moduleId);

        return messages
            .GroupBy(m => DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).Date)
            .Select(g => new DailyTrendDto
            {
                Date = g.Key,
                TotalMessages = g.Count(),
                UniqueStudents = g.Select(m => m.StudentId).Distinct().Count(),
                UniqueConversations = g.Select(m => m.ConversationId).Distinct().Count(),
                TotalTokens = g.Sum(m => m.TokenCount ?? 0),
                EstimatedCostUSD = (double)g.GroupBy(m => m.Provider).Sum(pg => CalculateProviderCost(pg.Key, pg.Sum(m => m.TokenCount ?? 0))),
                AverageResponseTime = g.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average()
            })
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<List<StudentActivitySummaryDto>> GetTopActiveStudentsAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null, int? moduleId = null)
    {
        var messages = await GetFilteredMessagesAsync(startDate, endDate, null, null, moduleId);

        return messages
            .Where(m => m.StudentId > 0) // Exclude anonymous users
            .GroupBy(m => m.StudentId)
            .Select(g => new StudentActivitySummaryDto
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
            .Take(limit)
            .ToList();
    }

    public async Task<ResponseQualityMetricsDto> GetResponseQualityMetricsAsync(DateTime? startDate = null, DateTime? endDate = null, int? moduleId = null)
    {
        var messages = await GetFilteredMessagesAsync(startDate, endDate, null, null, moduleId);

        var responseTimes = messages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).OrderBy(t => t).ToList();

        return new ResponseQualityMetricsDto
        {
            AverageResponseTime = responseTimes.DefaultIfEmpty(0).Average(),
            MedianResponseTime = responseTimes.Any() ? responseTimes[responseTimes.Count / 2] : 0,
            P95ResponseTime = responseTimes.Any() ? responseTimes[(int)(responseTimes.Count * 0.95)] : 0,
            P99ResponseTime = responseTimes.Any() ? responseTimes[(int)(responseTimes.Count * 0.99)] : 0,
            AverageTokensPerMessage = messages.Where(m => m.TokenCount.HasValue).Select(m => m.TokenCount!.Value).DefaultIfEmpty(0).Average(),
            TokenEfficiencyScore = responseTimes.Any() && responseTimes.Average() > 0 ? messages.Where(m => m.TokenCount.HasValue).Select(m => m.TokenCount!.Value).DefaultIfEmpty(0).Average() / responseTimes.Average() : 0,
            FastResponses = responseTimes.Count(t => t < 2000),
            SlowResponses = responseTimes.Count(t => t > 10000)
        };
    }

    public async Task<ConversationEngagementMetricsDto> GetConversationEngagementMetricsAsync(DateTime? startDate = null, DateTime? endDate = null, int? moduleId = null)
    {
        var messages = await GetFilteredMessagesAsync(startDate, endDate, null, null, moduleId);

        var conversationGroups = messages.GroupBy(m => m.ConversationId).ToList();
        var conversationLengths = conversationGroups.Select(g => g.Count()).OrderBy(c => c).ToList();

        return new ConversationEngagementMetricsDto
        {
            TotalConversations = conversationLengths.Count,
            AverageMessagesPerConversation = conversationLengths.DefaultIfEmpty(0).Average(),
            MedianMessagesPerConversation = conversationLengths.Any() ? conversationLengths[conversationLengths.Count / 2] : 0,
            SingleMessageConversations = conversationLengths.Count(c => c == 1),
            ShortConversations = conversationLengths.Count(c => c >= 2 && c <= 5),
            MediumConversations = conversationLengths.Count(c => c >= 6 && c <= 15),
            LongConversations = conversationLengths.Count(c => c >= 16),
            ConversationCompletionRate = conversationLengths.Count > 0 ? (conversationLengths.Count(c => c >= 3) * 100.0 / conversationLengths.Count) : 0,
            AverageConversationDuration = conversationGroups.Any() ? TimeSpan.FromMilliseconds(conversationGroups.Average(g => g.Max(m => m.Timestamp) - g.Min(m => m.Timestamp))) : TimeSpan.Zero
        };
    }

    public async Task<ModuleComparisonDto> GetModuleComparisonAsync(List<int> moduleIds, DateTime? startDate = null, DateTime? endDate = null)
    {
        var allModuleData = new List<ModuleComparisonItemDto>();

        foreach (var moduleId in moduleIds)
        {
            var messages = await GetFilteredMessagesAsync(startDate, endDate, null, null, moduleId);
            var uniqueStudents = messages.Select(m => m.StudentId).Distinct().Count();

            allModuleData.Add(new ModuleComparisonItemDto
            {
                ModuleId = moduleId,
                ModuleName = "", // TODO: Requires SQL join
                TotalMessages = messages.Count,
                UniqueStudents = uniqueStudents,
                AverageMessagesPerStudent = uniqueStudents > 0 ? (double)messages.Count / uniqueStudents : 0,
                AverageResponseTime = messages.Where(m => m.ResponseTime.HasValue).Select(m => m.ResponseTime!.Value).DefaultIfEmpty(0).Average(),
                TotalTokens = messages.Sum(m => m.TokenCount ?? 0),
                EstimatedCostUSD = (double)messages.GroupBy(m => m.Provider).Sum(g => CalculateProviderCost(g.Key, g.Sum(m => m.TokenCount ?? 0))),
                EngagementScore = uniqueStudents > 0 ? (double)messages.Count / uniqueStudents : 0 // Simple engagement metric
            });
        }

        return new ModuleComparisonDto
        {
            Modules = allModuleData,
            Insights = new Dictionary<string, object>
            {
                { "most_active_module", allModuleData.OrderByDescending(m => m.TotalMessages).FirstOrDefault()?.ModuleId ?? 0 },
                { "highest_engagement", allModuleData.OrderByDescending(m => m.EngagementScore).FirstOrDefault()?.ModuleId ?? 0 }
            }
        };
    }

    #endregion

    #region Helper Methods

    private async Task<List<ChatMessageDto>> GetFilteredMessagesAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? universityId,
        int? courseId,
        int? moduleId)
    {
        // If moduleId is provided, use ModuleAnalyticsIndex
        if (moduleId.HasValue)
        {
            return await GetModuleAnalyticsAsync(moduleId.Value, startDate, endDate, limit: 10000);
        }

        // Otherwise, we need to scan (not ideal, but necessary for university/course filtering)
        // In production, you'd want additional GSIs for university and course
        // For now, scan the table and filter in memory
        if (!_isEnabled || _dynamoDbClient == null)
        {
            _logger.LogWarning("DynamoDB not enabled, returning empty results");
            return new List<ChatMessageDto>();
        }

        try
        {
            var request = new ScanRequest
            {
                TableName = _tableName,
                Limit = 10000
            };

            var response = await _dynamoDbClient.ScanAsync(request);
            var allMessages = response.Items.Select(MapToChatMessageDto).ToList();

            // Filter by date
            if (startDate.HasValue)
            {
                var startTimestamp = new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds();
                allMessages = allMessages.Where(m => m.Timestamp >= startTimestamp).ToList();
            }

            if (endDate.HasValue)
            {
                var endTimestamp = new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds();
                allMessages = allMessages.Where(m => m.Timestamp <= endTimestamp).ToList();
            }

            // Note: University/course filtering would require joining with SQL database
            // This is a limitation of pure DynamoDB approach
            // In production, moduleId → courseId → universityId mapping should be cached

            return allMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning DynamoDB for analytics");
            return new List<ChatMessageDto>();
        }
    }

    private static decimal CalculateProviderCost(string provider, int totalTokens)
    {
        // Approximate pricing (as of 2025 - these are rough estimates)
        return provider.ToLower() switch
        {
            "openai" => totalTokens * 0.00001m, // ~$0.01 per 1K tokens average
            "anthropic" => totalTokens * 0.000015m, // ~$0.015 per 1K tokens average
            _ => totalTokens * 0.00001m
        };
    }

    private static decimal CalculateModelCost(string model, int totalTokens)
    {
        // Approximate pricing by model (rough estimates)
        return model.ToLower() switch
        {
            var m when m.Contains("gpt-4") => totalTokens * 0.00003m,
            var m when m.Contains("gpt-3.5") => totalTokens * 0.000002m,
            var m when m.Contains("claude-3-opus") => totalTokens * 0.000075m,
            var m when m.Contains("claude-3-sonnet") || m.Contains("claude-3-5-sonnet") => totalTokens * 0.000015m,
            var m when m.Contains("claude-3-haiku") => totalTokens * 0.00000125m,
            _ => totalTokens * 0.00001m
        };
    }

    #endregion

    private static ChatMessageDto MapToChatMessageDto(Dictionary<string, AttributeValue> item)
    {
        return new ChatMessageDto
        {
            ConversationId = item.ContainsKey("conversationId") ? item["conversationId"].S : string.Empty,
            Timestamp = item.ContainsKey("timestamp") && long.TryParse(item["timestamp"].N, out var ts) ? ts : 0,
            MessageId = item.ContainsKey("messageId") ? item["messageId"].S : string.Empty,
            StudentId = item.ContainsKey("studentId") && int.TryParse(item["studentId"].N, out var sid) ? sid : 0,
            ModuleId = item.ContainsKey("moduleId") && int.TryParse(item["moduleId"].N, out var mid) ? mid : 0,
            Question = item.ContainsKey("question") ? item["question"].S : string.Empty,
            Response = item.ContainsKey("response") ? item["response"].S : string.Empty,
            ModelUsed = item.ContainsKey("modelUsed") ? item["modelUsed"].S : string.Empty,
            Provider = item.ContainsKey("provider") ? item["provider"].S : string.Empty,
            HasFile = item.ContainsKey("hasFile") && (item["hasFile"].BOOL ?? false),
            FileName = item.ContainsKey("fileName") ? item["fileName"].S : null,
            TokenCount = item.ContainsKey("tokenCount") && int.TryParse(item["tokenCount"].N, out var tc) ? tc : null,
            ResponseTime = item.ContainsKey("responseTime") && int.TryParse(item["responseTime"].N, out var rt) ? rt : null,
            CreatedAt = item.ContainsKey("createdAt") ? item["createdAt"].S : string.Empty
        };
    }
}
