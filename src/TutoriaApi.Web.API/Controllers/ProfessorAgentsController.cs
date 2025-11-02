using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages Professor AI Agents - personalized AI assistants for professors
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfessorAgentsController : ControllerBase
{
    private readonly IProfessorAgentService _professorAgentService;
    private readonly ILogger<ProfessorAgentsController> _logger;

    public ProfessorAgentsController(
        IProfessorAgentService professorAgentService,
        ILogger<ProfessorAgentsController> logger)
    {
        _professorAgentService = professorAgentService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private string? GetCurrentUserType()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    [HttpGet("my-agent")]
    public async Task<ActionResult<ProfessorAgentDetailDto>> GetMyAgent()
    {
        try
        {
            var userId = GetCurrentUserId();
            var agent = await _professorAgentService.GetByProfessorIdAsync(userId);

            if (agent == null)
                return NotFound(new { message = "Professor agent not found" });

            var tokens = await _professorAgentService.GetTokensByAgentIdAsync(agent.Id);

            return Ok(new ProfessorAgentDetailDto
            {
                Id = agent.Id,
                ProfessorId = agent.ProfessorId,
                UniversityId = agent.UniversityId,
                Name = agent.Name,
                Description = agent.Description,
                SystemPrompt = agent.SystemPrompt,
                OpenAIAssistantId = agent.OpenAIAssistantId,
                OpenAIVectorStoreId = agent.OpenAIVectorStoreId,
                TutorLanguage = agent.TutorLanguage,
                AIModelId = agent.AIModelId,
                IsActive = agent.IsActive,
                Tokens = tokens.Select(t => new ProfessorAgentTokenListDto
                {
                    Id = t.Id,
                    Token = t.Token,
                    ProfessorAgentId = t.ProfessorAgentId,
                    ProfessorId = t.ProfessorId,
                    Name = t.Name,
                    Description = t.Description,
                    AllowChat = t.AllowChat,
                    ExpiresAt = t.ExpiresAt,
                    IsExpired = t.ExpiresAt.HasValue && t.ExpiresAt.Value < DateTime.UtcNow,
                    CreatedAt = t.CreatedAt
                }).ToList(),
                CreatedAt = agent.CreatedAt,
                UpdatedAt = agent.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving professor agent");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<IEnumerable<ProfessorAgentListDto>>> GetAllAgents([FromQuery] int? universityId = null)
    {
        try
        {
            var agents = await _professorAgentService.GetAllAgentsAsync(universityId);

            var result = agents.Select(a => new ProfessorAgentListDto
            {
                Id = a.Id,
                ProfessorId = a.ProfessorId,
                UniversityId = a.UniversityId,
                Name = a.Name,
                Description = a.Description,
                TutorLanguage = a.TutorLanguage,
                AIModelId = a.AIModelId,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving professor agents");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ProfessorAgentDetailDto>> CreateAgent([FromBody] ProfessorAgentCreateRequest request)
    {
        try
        {
            var agent = await _professorAgentService.CreateAgentAsync(
                request.ProfessorId,
                request.Name,
                request.Description,
                request.SystemPrompt,
                request.TutorLanguage,
                request.AIModelId);

            return CreatedAtAction(nameof(GetMyAgent), new ProfessorAgentDetailDto
            {
                Id = agent.Id,
                ProfessorId = agent.ProfessorId,
                UniversityId = agent.UniversityId,
                Name = agent.Name,
                Description = agent.Description,
                SystemPrompt = agent.SystemPrompt,
                TutorLanguage = agent.TutorLanguage,
                AIModelId = agent.AIModelId,
                IsActive = agent.IsActive,
                CreatedAt = agent.CreatedAt,
                UpdatedAt = agent.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating professor agent");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ProfessorAgentDetailDto>> UpdateAgent(int id, [FromBody] ProfessorAgentUpdateRequest request)
    {
        try
        {
            var agent = await _professorAgentService.UpdateAgentAsync(
                id,
                request.Name,
                request.Description,
                request.SystemPrompt,
                request.TutorLanguage,
                request.AIModelId,
                request.IsActive);

            return Ok(new ProfessorAgentDetailDto
            {
                Id = agent.Id,
                ProfessorId = agent.ProfessorId,
                UniversityId = agent.UniversityId,
                Name = agent.Name,
                Description = agent.Description,
                SystemPrompt = agent.SystemPrompt,
                TutorLanguage = agent.TutorLanguage,
                AIModelId = agent.AIModelId,
                IsActive = agent.IsActive,
                CreatedAt = agent.CreatedAt,
                UpdatedAt = agent.UpdatedAt
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Professor agent not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating professor agent with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult> DeleteAgent(int id)
    {
        try
        {
            await _professorAgentService.DeleteAgentAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Professor agent not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting professor agent with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost("{id}/tokens")]
    public async Task<ActionResult<ProfessorAgentTokenDetailDto>> CreateToken(int id, [FromBody] ProfessorAgentTokenCreateRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userType = GetCurrentUserType();

            var token = await _professorAgentService.CreateTokenAsync(
                id,
                userId,
                userType ?? string.Empty,
                request.Name,
                request.Description,
                request.AllowChat,
                request.ExpiresAt);

            return Ok(new ProfessorAgentTokenDetailDto
            {
                Id = token.Id,
                Token = token.Token,
                ProfessorAgentId = token.ProfessorAgentId,
                ProfessorId = token.ProfessorId,
                Name = token.Name,
                Description = token.Description,
                AllowChat = token.AllowChat,
                ExpiresAt = token.ExpiresAt,
                IsExpired = false,
                CreatedAt = token.CreatedAt,
                UpdatedAt = token.UpdatedAt
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Professor agent not found" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating professor agent token");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
