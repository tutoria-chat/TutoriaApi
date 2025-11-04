using System.Security.Cryptography;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class ProfessorAgentService : IProfessorAgentService
{
    private readonly IProfessorAgentRepository _professorAgentRepository;
    private readonly IProfessorAgentTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAIModelRepository _aiModelRepository;

    private static readonly string[] ValidLanguages = { "pt-br", "en", "es" };

    public ProfessorAgentService(
        IProfessorAgentRepository professorAgentRepository,
        IProfessorAgentTokenRepository tokenRepository,
        IUserRepository userRepository,
        IAIModelRepository aiModelRepository)
    {
        _professorAgentRepository = professorAgentRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _aiModelRepository = aiModelRepository;
    }

    public async Task<ProfessorAgent?> GetByProfessorIdAsync(int professorId)
    {
        return await _professorAgentRepository.GetByProfessorIdAsync(professorId);
    }

    public async Task<IEnumerable<ProfessorAgent>> GetAllAgentsAsync(int? universityId = null)
    {
        if (universityId.HasValue)
        {
            return await _professorAgentRepository.GetByUniversityIdAsync(universityId.Value);
        }

        return await _professorAgentRepository.GetActiveAgentsAsync();
    }

    public async Task<IEnumerable<ProfessorAgentStatusDto>> GetProfessorAgentStatusAsync(int? universityId = null)
    {
        // Get all professors (optionally filtered by university)
        var professors = universityId.HasValue
            ? (await _userRepository.GetByUniversityIdAsync(universityId.Value))
                .Where(u => u.UserType == "professor")
            : await _userRepository.GetByTypeAsync("professor");

        // Get all agents
        var agents = await _professorAgentRepository.GetAllAsync();
        var agentDict = agents.ToDictionary(a => a.ProfessorId, a => a);

        // Map to status DTOs
        var result = professors.Select(prof =>
        {
            var hasAgent = agentDict.TryGetValue(prof.UserId, out var agent);
            return new ProfessorAgentStatusDto
            {
                ProfessorId = prof.UserId,
                ProfessorName = $"{prof.FirstName} {prof.LastName}",
                ProfessorEmail = prof.Email,
                HasAgent = hasAgent,
                AgentId = hasAgent ? agent?.Id : null,
                AgentName = hasAgent ? agent?.Name : null,
                AgentIsActive = hasAgent ? agent?.IsActive : null,
                AgentCreatedAt = hasAgent ? agent?.CreatedAt : null
            };
        }).ToList();

        return result;
    }

    public async Task DeactivateAgentAsync(int id)
    {
        var agent = await _professorAgentRepository.GetByIdAsync(id);
        if (agent == null)
        {
            throw new KeyNotFoundException("Professor agent not found");
        }

        agent.IsActive = false;
        agent.UpdatedAt = DateTime.UtcNow;

        await _professorAgentRepository.UpdateAsync(agent);
    }

    public async Task ActivateAgentAsync(int id)
    {
        var agent = await _professorAgentRepository.GetByIdAsync(id);
        if (agent == null)
        {
            throw new KeyNotFoundException("Professor agent not found");
        }

        agent.IsActive = true;
        agent.UpdatedAt = DateTime.UtcNow;

        await _professorAgentRepository.UpdateAsync(agent);
    }

    public async Task<ProfessorAgent> CreateAgentAsync(
        int professorId,
        string name,
        string? description,
        string? systemPrompt,
        string? tutorLanguage,
        int? aiModelId)
    {
        // Get professor to get university ID
        var professor = await _userRepository.GetByIdAsync(professorId);
        if (professor == null || professor.UserType != "professor")
        {
            throw new InvalidOperationException("Invalid professor ID");
        }

        if (!professor.UniversityId.HasValue)
        {
            throw new InvalidOperationException("Professor must be associated with a university");
        }

        // Check if agent already exists for this professor
        var existing = await _professorAgentRepository.GetByProfessorIdAsync(professorId);
        if (existing != null)
        {
            throw new InvalidOperationException("Professor already has an agent");
        }

        var agent = new ProfessorAgent
        {
            ProfessorId = professorId,
            UniversityId = professor.UniversityId.Value,
            Name = name,
            Description = description,
            SystemPrompt = systemPrompt ?? GetDefaultSystemPrompt(),
            TutorLanguage = tutorLanguage ?? professor.LanguagePreference,
            AIModelId = aiModelId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _professorAgentRepository.AddAsync(agent);
    }

    public async Task<ProfessorAgent> UpdateAgentAsync(
        int id,
        string? name,
        string? description,
        string? systemPrompt,
        string? tutorLanguage,
        int? aiModelId,
        bool? isActive)
    {
        var agent = await _professorAgentRepository.GetByIdAsync(id);
        if (agent == null)
        {
            throw new KeyNotFoundException("Professor agent not found");
        }

        // Validate name - prevent empty/whitespace strings
        if (name != null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Agent name cannot be empty or whitespace", nameof(name));
            }
            agent.Name = name;
        }

        // Description can be empty (optional field)
        if (description != null) agent.Description = description;

        // Validate systemPrompt - prevent empty/whitespace strings
        if (systemPrompt != null)
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new ArgumentException("System prompt cannot be empty or whitespace", nameof(systemPrompt));
            }
            agent.SystemPrompt = systemPrompt;
        }

        // Validate tutorLanguage - must be one of the valid languages
        if (tutorLanguage != null)
        {
            if (string.IsNullOrWhiteSpace(tutorLanguage))
            {
                throw new ArgumentException("Tutor language cannot be empty or whitespace", nameof(tutorLanguage));
            }

            if (!ValidLanguages.Contains(tutorLanguage))
            {
                throw new ArgumentException(
                    $"Invalid tutor language '{tutorLanguage}'. Valid languages are: {string.Join(", ", ValidLanguages)}",
                    nameof(tutorLanguage));
            }

            agent.TutorLanguage = tutorLanguage;
        }

        // Validate aiModelId - must exist in database
        if (aiModelId.HasValue)
        {
            var aiModel = await _aiModelRepository.GetByIdAsync(aiModelId.Value);
            if (aiModel == null)
            {
                throw new ArgumentException($"AI Model with ID {aiModelId.Value} not found", nameof(aiModelId));
            }
            agent.AIModelId = aiModelId;
        }

        if (isActive.HasValue) agent.IsActive = isActive.Value;

        agent.UpdatedAt = DateTime.UtcNow;

        await _professorAgentRepository.UpdateAsync(agent);

        return agent;
    }

    public async Task DeleteAgentAsync(int id)
    {
        var agent = await _professorAgentRepository.GetByIdAsync(id);
        if (agent == null)
        {
            throw new KeyNotFoundException("Professor agent not found");
        }

        await _professorAgentRepository.DeleteAsync(agent);
    }

    public async Task<ProfessorAgentToken> CreateTokenAsync(
        int agentId,
        int currentUserId,
        string currentUserType,
        string name,
        string? description,
        bool allowChat,
        DateTime? expiresAt)
    {
        var agent = await _professorAgentRepository.GetByIdAsync(agentId);
        if (agent == null)
        {
            throw new KeyNotFoundException("Professor agent not found");
        }

        // Check authorization: must be the owner or admin
        if (agent.ProfessorId != currentUserId && currentUserType != "super_admin" && currentUserType != "admin_professor")
        {
            throw new UnauthorizedAccessException("User is not authorized to create tokens for this agent");
        }

        var token = new ProfessorAgentToken
        {
            Token = GenerateSecureToken(),
            ProfessorAgentId = agentId,
            ProfessorId = agent.ProfessorId,
            Name = name,
            Description = description,
            AllowChat = allowChat,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _tokenRepository.AddAsync(token);
    }

    public async Task<IEnumerable<ProfessorAgentToken>> GetTokensByAgentIdAsync(int agentId)
    {
        return await _tokenRepository.GetByProfessorAgentIdAsync(agentId);
    }

    private string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
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
