using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.Management.Controllers;

/// <summary>
/// Provides analytics and insights from DynamoDB chat data.
/// </summary>
/// <remarks>
/// This controller queries DynamoDB to provide usage analytics, conversation history,
/// student activity tracking, and FAQ generation based on common questions.
///
/// **Authorization**: All endpoints require authentication. Most endpoints require ProfessorOrAbove policy.
///
/// **Key Features**:
/// - Module usage analytics and summaries
/// - Student activity tracking
/// - Conversation history retrieval
/// - AI provider/model usage statistics
/// - FAQ generation from common questions
///
/// **Note**: DynamoDB must be configured and enabled in appsettings for these endpoints to work.
/// When disabled, endpoints return empty results with appropriate warnings in logs.
/// </remarks>
[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IDynamoDbAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IDynamoDbAnalyticsService analyticsService,
        ILogger<AnalyticsController> _logger)
    {
        _analyticsService = analyticsService;
        this._logger = _logger;
    }

    /// <summary>
    /// Get conversation history for a specific conversation ID
    /// </summary>
    /// <param name="conversationId">The UUID of the conversation</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 50)</param>
    /// <returns>List of chat messages in chronological order</returns>
    [HttpGet("conversations/{conversationId}")]
    [Authorize(Policy = "ProfessorOrAbove")]
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

            var messages = await _analyticsService.GetConversationHistoryAsync(conversationId, limit);

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
    /// <param name="moduleId">The ID of the module</param>
    /// <param name="startDate">Optional start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date for filtering (ISO 8601 format)</param>
    /// <returns>Summary statistics including total messages, unique students, response times, etc.</returns>
    [HttpGet("modules/{moduleId}/summary")]
    [Authorize(Policy = "ProfessorOrAbove")]
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

            var summary = await _analyticsService.GetModuleSummaryAsync(moduleId, startDate, endDate);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module summary for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get detailed chat messages for a module
    /// </summary>
    /// <param name="moduleId">The ID of the module</param>
    /// <param name="startDate">Optional start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date for filtering (ISO 8601 format)</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 1000)</param>
    /// <returns>List of chat messages for the module</returns>
    [HttpGet("modules/{moduleId}/messages")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetModuleMessages(
        int moduleId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 1000)
    {
        if (moduleId < 1)
        {
            return BadRequest(new { message = "Invalid module ID" });
        }

        if (limit < 1 || limit > 5000)
        {
            return BadRequest(new { message = "Limit must be between 1 and 5000" });
        }

        try
        {
            _logger.LogInformation("Retrieving messages for module {ModuleId}", moduleId);

            var messages = await _analyticsService.GetModuleAnalyticsAsync(moduleId, startDate, endDate, limit);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get chat activity for a specific student
    /// </summary>
    /// <param name="studentId">The ID of the student</param>
    /// <param name="startDate">Optional start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date for filtering (ISO 8601 format)</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 100)</param>
    /// <returns>List of chat messages from the student</returns>
    [HttpGet("students/{studentId}/activity")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetStudentActivity(
        int studentId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        if (studentId < 1)
        {
            return BadRequest(new { message = "Invalid student ID" });
        }

        if (limit < 1 || limit > 1000)
        {
            return BadRequest(new { message = "Limit must be between 1 and 1000" });
        }

        try
        {
            _logger.LogInformation("Retrieving activity for student {StudentId}", studentId);

            var messages = await _analyticsService.GetStudentActivityAsync(studentId, startDate, endDate, limit);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity for student {StudentId}", studentId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get usage statistics for a specific AI provider (OpenAI or Anthropic)
    /// </summary>
    /// <param name="provider">The provider name (openai or anthropic)</param>
    /// <param name="startDate">Optional start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date for filtering (ISO 8601 format)</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 1000)</param>
    /// <returns>List of chat messages using the specified provider</returns>
    [HttpGet("providers/{provider}/usage")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetProviderUsage(
        string provider,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 1000)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest(new { message = "Provider is required" });
        }

        var normalizedProvider = provider.ToLowerInvariant();
        if (normalizedProvider != "openai" && normalizedProvider != "anthropic")
        {
            return BadRequest(new { message = "Provider must be 'openai' or 'anthropic'" });
        }

        if (limit < 1 || limit > 5000)
        {
            return BadRequest(new { message = "Limit must be between 1 and 5000" });
        }

        try
        {
            _logger.LogInformation("Retrieving usage statistics for provider {Provider}", provider);

            var messages = await _analyticsService.GetProviderUsageAsync(normalizedProvider, startDate, endDate, limit);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider usage for {Provider}", provider);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Generate FAQ (Frequently Asked Questions) from common questions for a module
    /// </summary>
    /// <param name="moduleId">The ID of the module</param>
    /// <param name="minimumOccurrences">Minimum number of times a question must appear (default: 3)</param>
    /// <param name="maxResults">Maximum number of FAQ items to return (default: 10)</param>
    /// <returns>List of FAQ items with questions, answers, and occurrence counts</returns>
    [HttpGet("modules/{moduleId}/faq")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<List<FaqItemDto>>> GenerateFaq(
        int moduleId,
        [FromQuery] int minimumOccurrences = 3,
        [FromQuery] int maxResults = 10)
    {
        if (moduleId < 1)
        {
            return BadRequest(new { message = "Invalid module ID" });
        }

        if (minimumOccurrences < 1 || minimumOccurrences > 100)
        {
            return BadRequest(new { message = "Minimum occurrences must be between 1 and 100" });
        }

        if (maxResults < 1 || maxResults > 50)
        {
            return BadRequest(new { message = "Max results must be between 1 and 50" });
        }

        try
        {
            _logger.LogInformation("Generating FAQ for module {ModuleId} with min occurrences {MinOccurrences}",
                moduleId, minimumOccurrences);

            var faqItems = await _analyticsService.GenerateFaqFromQuestionsAsync(moduleId, minimumOccurrences, maxResults);

            return Ok(faqItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating FAQ for module {ModuleId}", moduleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get cost analysis summary for AI provider usage
    /// </summary>
    /// <param name="startDate">Optional start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">Optional end date for filtering (ISO 8601 format)</param>
    /// <returns>Summary of costs by provider and model</returns>
    [HttpGet("costs")]
    [Authorize(Policy = "ProfessorOrAbove")]
    public async Task<ActionResult<object>> GetCostAnalysis(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformation("Retrieving cost analysis");

            var summary = await _analyticsService.GetCostAnalysisAsync(startDate, endDate);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost analysis");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
