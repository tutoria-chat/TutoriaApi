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
    private readonly IProfessorAgentRepository _professorAgentRepository;
    private readonly IProfessorAgentTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ProfessorAgentsController> _logger;

    public ProfessorAgentsController(
        IProfessorAgentRepository professorAgentRepository,
        IProfessorAgentTokenRepository tokenRepository,
        IUserRepository userRepository,
        ILogger<ProfessorAgentsController> logger)
    {
        _professorAgentRepository = professorAgentRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
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
            var agent = await _professorAgentRepository.GetByProfessorIdAsync(userId);

            if (agent == null)
                return NotFound(new { message = "Professor agent not found" });

            var tokens = await _tokenRepository.GetByProfessorAgentIdAsync(agent.Id);

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
            var agents = universityId.HasValue
                ? await _professorAgentRepository.GetByUniversityIdAsync(universityId.Value)
                : await _professorAgentRepository.GetActiveAgentsAsync();

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
            // Get professor to get university ID
            var professor = await _userRepository.GetByIdAsync(request.ProfessorId);
            if (professor == null || professor.UserType != "professor")
                return BadRequest(new { message = "Invalid professor ID" });

            if (!professor.UniversityId.HasValue)
                return BadRequest(new { message = "Professor must be associated with a university" });

            // Check if agent already exists for this professor
            var existing = await _professorAgentRepository.GetByProfessorIdAsync(request.ProfessorId);
            if (existing != null)
                return BadRequest(new { message = "Professor already has an agent" });

            var agent = new ProfessorAgent
            {
                ProfessorId = request.ProfessorId,
                UniversityId = professor.UniversityId.Value,
                Name = request.Name,
                Description = request.Description,
                SystemPrompt = request.SystemPrompt ?? GetDefaultSystemPrompt(),
                TutorLanguage = request.TutorLanguage,
                AIModelId = request.AIModelId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _professorAgentRepository.AddAsync(agent);

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
            var agent = await _professorAgentRepository.GetByIdAsync(id);
            if (agent == null)
                return NotFound(new { message = "Professor agent not found" });

            if (request.Name != null) agent.Name = request.Name;
            if (request.Description != null) agent.Description = request.Description;
            if (request.SystemPrompt != null) agent.SystemPrompt = request.SystemPrompt;
            if (request.TutorLanguage != null) agent.TutorLanguage = request.TutorLanguage;
            if (request.AIModelId.HasValue) agent.AIModelId = request.AIModelId;
            if (request.IsActive.HasValue) agent.IsActive = request.IsActive.Value;

            await _professorAgentRepository.UpdateAsync(agent);

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
            var agent = await _professorAgentRepository.GetByIdAsync(id);
            if (agent == null)
                return NotFound(new { message = "Professor agent not found" });

            await _professorAgentRepository.DeleteAsync(id);

            return NoContent();
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
            var agent = await _professorAgentRepository.GetByIdAsync(id);

            if (agent == null)
                return NotFound(new { message = "Professor agent not found" });

            // Check authorization: must be the owner or admin
            var userType = GetCurrentUserType();
            if (agent.ProfessorId != userId && userType != "super_admin" && userType != "admin_professor")
                return Forbid();

            var token = new ProfessorAgentToken
            {
                Token = GenerateSecureToken(),
                ProfessorAgentId = id,
                ProfessorId = agent.ProfessorId,
                Name = request.Name,
                Description = request.Description,
                AllowChat = request.AllowChat,
                ExpiresAt = request.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _tokenRepository.AddAsync(token);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating professor agent token");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    private string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    private string GetDefaultSystemPrompt()
    {
        return @"You are an AI assistant helping a professor analyze and improve their course materials.

Your role:
- Review course content and provide constructive feedback
- Suggest improvements to clarity, structure, and pedagogy
- Identify potential gaps in course coverage
- Recommend additional resources or topics
- Help align content with learning objectives

Be professional, constructive, and supportive in your feedback.";
    }
}
