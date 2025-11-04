using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Provides comprehensive analytics and insights with role-based access control
/// </summary>
/// <remarks>
/// This controller provides analytics endpoints with automatic role-based filtering:
/// - **SuperAdmin**: Can see all universities' data
/// - **ProfessorAdmin**: Can only see their university's data
/// - **Professor**: Can only see their assigned courses' data
///
/// All endpoints require authentication and implement the ProfessorOrAbove policy.
/// </remarks>
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "ProfessorOrAbove")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IDynamoDbAnalyticsService _dynamoDbService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        IDynamoDbAnalyticsService dynamoDbService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _dynamoDbService = dynamoDbService;
        _logger = logger;
    }

    #region Cost Analysis Endpoints

    /// <summary>
    /// Get comprehensive cost breakdown with hierarchical filtering
    /// </summary>
    /// <remarks>
    /// SuperAdmin: Can filter by any university/course/module
    /// ProfessorAdmin: Can only see their university's data
    /// Professor: Can only see their assigned courses' data
    /// </remarks>
    [HttpGet("costs/detailed")]
    [ProducesResponseType(typeof(CostAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CostAnalysisDto>> GetCostAnalysis(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                UniversityId = universityId,
                CourseId = courseId,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to cost analysis by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost analysis");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get today's costs with real-time updates and comparison to yesterday
    /// </summary>
    [HttpGet("costs/today")]
    [ProducesResponseType(typeof(TodayCostDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TodayCostDto>> GetTodayCost(
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                UniversityId = universityId,
                CourseId = courseId,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetTodayCostAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to today's cost by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's cost");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Usage Statistics Endpoints

    /// <summary>
    /// Get comprehensive today's usage stats with real-time updates
    /// </summary>
    [HttpGet("usage/today")]
    [ProducesResponseType(typeof(UsageStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UsageStatsDto>> GetTodayUsageStats(
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                UniversityId = universityId,
                CourseId = courseId,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetTodayUsageStatsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to usage stats by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's usage stats");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get usage trends over time (daily aggregation)
    /// </summary>
    [HttpGet("usage/trends")]
    [ProducesResponseType(typeof(UsageTrendsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UsageTrendsResponseDto>> GetUsageTrends(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                UniversityId = universityId,
                CourseId = courseId,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetUsageTrendsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to usage trends by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage trends");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get hourly usage breakdown for peak time analysis
    /// </summary>
    [HttpGet("usage/hourly")]
    [ProducesResponseType(typeof(HourlyUsageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HourlyUsageResponseDto>> GetHourlyUsage(
        [FromQuery] DateTime? date = null,
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                UniversityId = universityId,
                CourseId = courseId,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetHourlyUsageAsync(userId, userRole, userUniversityId, date, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to hourly usage by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hourly usage");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Student & Engagement Endpoints

    /// <summary>
    /// Get top active students by message count
    /// </summary>
    [HttpGet("students/top-active")]
    [ProducesResponseType(typeof(TopActiveStudentsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TopActiveStudentsResponseDto>> GetTopActiveStudents(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { message = "Limit must be between 1 and 100" });
            }

            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new TopStudentsFilterDto
            {
                Limit = limit,
                StartDate = startDate,
                EndDate = endDate,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetTopActiveStudentsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to top students by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top active students");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get conversation engagement metrics (avg messages per conversation, completion rate)
    /// </summary>
    [HttpGet("engagement/conversations")]
    [ProducesResponseType(typeof(ConversationMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConversationMetricsDto>> GetConversationEngagementMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetConversationEngagementMetricsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to conversation metrics by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation engagement metrics");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Performance & Quality Endpoints

    /// <summary>
    /// Get response quality and performance metrics (response time, token efficiency)
    /// </summary>
    [HttpGet("performance/response-quality")]
    [ProducesResponseType(typeof(ResponseQualityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResponseQualityDto>> GetResponseQualityMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetResponseQualityMetricsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to response quality metrics by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response quality metrics");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Module Comparison Endpoints

    /// <summary>
    /// Compare multiple modules side-by-side
    /// </summary>
    [HttpGet("modules/compare")]
    [ProducesResponseType(typeof(ModuleComparisonResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ModuleComparisonResponseDto>> GetModuleComparison(
        [FromQuery] string moduleIds, // Comma-separated list (e.g., "1,2,3,4")
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(moduleIds))
            {
                return BadRequest(new { message = "moduleIds parameter is required" });
            }

            List<int> moduleIdList;
            try
            {
                moduleIdList = moduleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.Parse(id.Trim()))
                    .ToList();
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Invalid moduleIds format. Expected comma-separated integers (e.g., '1,2,3')" });
            }

            if (moduleIdList.Count < 2)
            {
                return BadRequest(new { message = "At least 2 module IDs are required for comparison" });
            }

            if (moduleIdList.Count > 10)
            {
                return BadRequest(new { message = "Maximum 10 modules can be compared at once" });
            }

            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new ModuleComparisonFilterDto
            {
                ModuleIds = moduleIdList,
                StartDate = startDate,
                EndDate = endDate
            };

            var result = await _analyticsService.GetModuleComparisonAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to module comparison by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to one or more of the requested modules" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module comparison");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Dashboard Endpoints

    /// <summary>
    /// Get high-level executive dashboard summary
    /// </summary>
    /// <remarks>
    /// Period values: "today", "week", "month", "quarter", "year"
    /// </remarks>
    [HttpGet("dashboard/summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary(
        [FromQuery] string period = "month",
        [FromQuery] int? universityId = null)
    {
        try
        {
            var validPeriods = new[] { "today", "week", "month", "quarter", "year" };
            if (!validPeriods.Contains(period.ToLower()))
            {
                return BadRequest(new { message = $"Invalid period. Must be one of: {string.Join(", ", validPeriods)}" });
            }

            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new DashboardFilterDto
            {
                Period = period.ToLower(),
                UniversityId = universityId
            };

            var result = await _analyticsService.GetDashboardSummaryAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to dashboard summary by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard summary");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get unified dashboard data in a single request (optimized for frontend)
    /// </summary>
    /// <remarks>
    /// This endpoint combines 4 separate analytics calls into a single request to reduce network overhead.
    /// Returns: Dashboard Summary + Usage Trends + Today's Usage + Today's Cost
    ///
    /// **Performance Optimization:**
    /// - Reduces 4 HTTP requests to 1
    /// - Backend can optimize shared computations
    /// - Recommended for dashboard page loads
    ///
    /// **Date Range:**
    /// - If not specified, defaults to last 30 days
    /// - Format: YYYY-MM-DD
    /// </remarks>
    [HttpGet("dashboard/unified")]
    [ProducesResponseType(typeof(UnifiedDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UnifiedDashboardResponseDto>> GetUnifiedDashboard(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? universityId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            // Build filters for all analytics calls
            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                UniversityId = universityId
            };

            var dashboardFilters = new DashboardFilterDto
            {
                Period = "month",
                UniversityId = universityId
            };

            // Execute all 4 analytics calls in parallel to optimize performance
            // Use Task.WhenAll for proper parallel execution
            var summaryTask = _analyticsService.GetDashboardSummaryAsync(userId, userRole, userUniversityId, dashboardFilters);
            var trendsTask = _analyticsService.GetUsageTrendsAsync(userId, userRole, userUniversityId, filters);
            var todayUsageTask = _analyticsService.GetTodayUsageStatsAsync(userId, userRole, userUniversityId, filters);
            var todayCostTask = _analyticsService.GetTodayCostAsync(userId, userRole, userUniversityId, filters);

            await Task.WhenAll(summaryTask, trendsTask, todayUsageTask, todayCostTask);

            var summary = await summaryTask;
            var trends = await trendsTask;
            var todayUsage = await todayUsageTask;
            var todayCost = await todayCostTask;

            var response = new UnifiedDashboardResponseDto
            {
                Summary = summary,
                Trends = trends,
                TodayUsage = todayUsage,
                TodayCost = todayCost
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to unified dashboard by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments for unified dashboard");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unified dashboard data");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Frequently Asked Questions Endpoints

    /// <summary>
    /// Get most frequently asked questions by students
    /// </summary>
    [HttpGet("questions/frequently-asked")]
    [ProducesResponseType(typeof(FrequentlyAskedQuestionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FrequentlyAskedQuestionsResponseDto>> GetFrequentlyAskedQuestions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? moduleId = null)
    {
        try
        {
            var (userId, userRole, userUniversityId) = GetUserContext();

            var filters = new AnalyticsFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                ModuleId = moduleId
            };

            var result = await _analyticsService.GetFrequentlyAskedQuestionsAsync(userId, userRole, userUniversityId, filters);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to FAQs by user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Unauthorized(new { message = "You do not have access to this resource" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting frequently asked questions");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Legacy Endpoints (Direct DynamoDB Access)

    /// <summary>
    /// Get conversation history for a specific conversation ID
    /// </summary>
    [HttpGet("conversations/{conversationId}")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetConversationHistory(
        string conversationId,
        [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(new { message = "Conversation ID is required" });
        }

        if (limit < 1 || limit > 500)
        {
            return BadRequest(new { message = "Limit must be between 1 and 500" });
        }

        try
        {
            _logger.LogInformation("Retrieving conversation history for {ConversationId}", conversationId);
            var messages = await _dynamoDbService.GetConversationHistoryAsync(conversationId, limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for {ConversationId}", conversationId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get aggregated analytics summary for a module
    /// </summary>
    [HttpGet("modules/{moduleId}/summary")]
    public async Task<ActionResult<ModuleAnalyticsSummaryDto>> GetModuleSummary(
        int moduleId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (moduleId < 1)
        {
            return BadRequest(new { message = "Invalid module ID" });
        }

        try
        {
            _logger.LogInformation("Retrieving analytics summary for module {ModuleId}", moduleId);
            var summary = await _dynamoDbService.GetModuleSummaryAsync(moduleId, startDate, endDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module summary for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extract user context from JWT claims
    /// </summary>
    private (int userId, string userRole, int? userUniversityId) GetUserContext()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        var universityIdClaim = User.FindFirst("university_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }

        if (string.IsNullOrEmpty(userRoleClaim))
        {
            throw new UnauthorizedAccessException("Invalid user role in token");
        }

        int? universityId = null;
        if (!string.IsNullOrEmpty(universityIdClaim) && int.TryParse(universityIdClaim, out var univId))
        {
            universityId = univId;
        }

        return (userId, userRoleClaim, universityId);
    }

    #endregion
}
