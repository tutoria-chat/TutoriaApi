using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;

namespace TutoriaApi.Tests.Unit.Services;

public class ProfessorAgentServiceTests
{
    private readonly Mock<IProfessorAgentRepository> _agentRepositoryMock;
    private readonly Mock<IProfessorAgentTokenRepository> _tokenRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IAIModelRepository> _aiModelRepositoryMock;
    private readonly ProfessorAgentService _service;

    public ProfessorAgentServiceTests()
    {
        _agentRepositoryMock = new Mock<IProfessorAgentRepository>();
        _tokenRepositoryMock = new Mock<IProfessorAgentTokenRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _aiModelRepositoryMock = new Mock<IAIModelRepository>();

        _service = new ProfessorAgentService(
            _agentRepositoryMock.Object,
            _tokenRepositoryMock.Object,
            _userRepositoryMock.Object,
            _aiModelRepositoryMock.Object);
    }

    #region GetByProfessorIdAsync Tests

    [Fact]
    public async Task GetByProfessorIdAsync_ExistingProfessor_ReturnsAgent()
    {
        // Arrange
        var professorId = 1;
        var agent = new ProfessorAgent
        {
            Id = 1,
            ProfessorId = professorId,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdAsync(professorId))
            .ReturnsAsync(agent);

        // Act
        var result = await _service.GetByProfessorIdAsync(professorId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(professorId, result.ProfessorId);
        _agentRepositoryMock.Verify(r => r.GetByProfessorIdAsync(professorId), Times.Once);
    }

    [Fact]
    public async Task GetByProfessorIdAsync_NonExistentProfessor_ReturnsNull()
    {
        // Arrange
        var professorId = 999;
        _agentRepositoryMock.Setup(r => r.GetByProfessorIdAsync(professorId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act
        var result = await _service.GetByProfessorIdAsync(professorId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetAllAgentsAsync Tests

    [Fact]
    public async Task GetAllAgentsAsync_WithUniversityId_ReturnsFilteredAgents()
    {
        // Arrange
        var universityId = 1;
        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent { Id = 1, UniversityId = universityId, Name = "Agent 1", IsActive = true },
            new ProfessorAgent { Id = 2, UniversityId = universityId, Name = "Agent 2", IsActive = true }
        };

        _agentRepositoryMock.Setup(r => r.GetByUniversityIdAsync(universityId))
            .ReturnsAsync(agents);

        // Act
        var result = await _service.GetAllAgentsAsync(universityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _agentRepositoryMock.Verify(r => r.GetByUniversityIdAsync(universityId), Times.Once);
    }

    [Fact]
    public async Task GetAllAgentsAsync_WithoutUniversityId_ReturnsActiveAgents()
    {
        // Arrange
        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent { Id = 1, Name = "Agent 1", IsActive = true },
            new ProfessorAgent { Id = 2, Name = "Agent 2", IsActive = true }
        };

        _agentRepositoryMock.Setup(r => r.GetActiveAgentsAsync())
            .ReturnsAsync(agents);

        // Act
        var result = await _service.GetAllAgentsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _agentRepositoryMock.Verify(r => r.GetActiveAgentsAsync(), Times.Once);
    }

    #endregion

    #region CreateAgentAsync Tests

    [Fact]
    public async Task CreateAgentAsync_ValidProfessor_CreatesAndReturnsAgent()
    {
        // Arrange
        var professorId = 1;
        var professor = new User
        {
            UserId = professorId,
            Username = "testprof",
            Email = "test@prof.com",
            FirstName = "Test",
            LastName = "Prof",
            UserType = "professor",
            UniversityId = 1,
            LanguagePreference = "pt-br"
        };

        var aiModel = new AIModel { Id = 1, ModelName = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Provider = "openai" };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdIncludingInactiveAsync(professorId))
            .ReturnsAsync((ProfessorAgent?)null);

        _aiModelRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(aiModel);

        _agentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<ProfessorAgent>()))
            .ReturnsAsync((ProfessorAgent agent) => { agent.Id = 1; return agent; });

        // Act
        var result = await _service.CreateAgentAsync(
            professorId,
            "Test Agent",
            "Description",
            null,
            "en",
            1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(professorId, result.ProfessorId);
        Assert.Equal("Test Agent", result.Name);
        Assert.Equal("en", result.TutorLanguage); // Explicitly provided language is used
        Assert.Equal(1, result.UniversityId);
        _agentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ProfessorAgent>()), Times.Once);
    }

    [Fact]
    public async Task CreateAgentAsync_NullTutorLanguage_UsesProfessorLanguagePreference()
    {
        // Arrange
        var professorId = 1;
        var professor = new User
        {
            UserId = professorId,
            Username = "testprof",
            Email = "test@prof.com",
            FirstName = "Test",
            LastName = "Prof",
            UserType = "professor",
            UniversityId = 1,
            LanguagePreference = "es" // Professor prefers Spanish
        };

        var aiModel = new AIModel { Id = 1, ModelName = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Provider = "openai" };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdIncludingInactiveAsync(professorId))
            .ReturnsAsync((ProfessorAgent?)null);

        _aiModelRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(aiModel);

        _agentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<ProfessorAgent>()))
            .ReturnsAsync((ProfessorAgent agent) => { agent.Id = 1; return agent; });

        // Act
        var result = await _service.CreateAgentAsync(
            professorId,
            "Test Agent",
            "Description",
            null,
            null, // No language specified - should use professor's preference
            1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("es", result.TutorLanguage); // Should use professor's preference
        _agentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ProfessorAgent>()), Times.Once);
    }

    [Fact]
    public async Task CreateAgentAsync_ProfessorNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var professorId = 999;
        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAgentAsync(professorId, "Test Agent", null, null, "en", null));
    }

    [Fact]
    public async Task CreateAgentAsync_UserNotProfessor_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = 1;
        var student = new User
        {
            UserId = userId,
            Username = "student",
            Email = "student@test.com",
            FirstName = "Test",
            LastName = "Student",
            UserType = "student",
            LanguagePreference = "pt-br"
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(student);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAgentAsync(userId, "Test Agent", null, null, null, null));
    }

    [Fact]
    public async Task CreateAgentAsync_ProfessorWithoutUniversity_ThrowsInvalidOperationException()
    {
        // Arrange
        var professorId = 1;
        var professor = new User
        {
            UserId = professorId,
            Username = "testprof",
            Email = "test@prof.com",
            FirstName = "Test",
            LastName = "Prof",
            UserType = "professor",
            UniversityId = null,
            LanguagePreference = "pt-br"
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAgentAsync(professorId, "Test Agent", null, null, null, null));
    }

    [Fact]
    public async Task CreateAgentAsync_AgentAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var professorId = 1;
        var professor = new User
        {
            UserId = professorId,
            Username = "testprof",
            Email = "test@prof.com",
            FirstName = "Test",
            LastName = "Prof",
            UserType = "professor",
            UniversityId = 1,
            LanguagePreference = "pt-br"
        };

        var existingAgent = new ProfessorAgent
        {
            Id = 1,
            ProfessorId = professorId,
            UniversityId = 1,
            Name = "Existing Agent",
            IsActive = true
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdIncludingInactiveAsync(professorId))
            .ReturnsAsync(existingAgent);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAgentAsync(professorId, "Test Agent", null, null, null, null));
    }

    #endregion

    #region UpdateAgentAsync Tests

    [Fact]
    public async Task UpdateAgentAsync_ValidAgent_UpdatesAndReturnsAgent()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Old Name",
            Description = "Old Description",
            SystemPrompt = "Old Prompt",
            TutorLanguage = "en",
            IsActive = true
        };

        var aiModel = new AIModel
        {
            Id = 2,
            ModelName = "gpt-4o",
            DisplayName = "GPT-4o",
            Provider = "openai",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _aiModelRepositoryMock.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(aiModel);

        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAgentAsync(
            agentId,
            1, // currentUserId
            "super_admin", // currentUserType
            1, // currentUserUniversityId
            "New Name",
            "New Description",
            "New Prompt",
            "es",
            2,
            false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("New Description", result.Description);
        Assert.Equal("New Prompt", result.SystemPrompt);
        Assert.Equal("es", result.TutorLanguage);
        Assert.Equal(2, result.AIModelId);
        Assert.False(result.IsActive);
        _agentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAgentAsync_NonExistentAgent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var agentId = 999;
        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, "New Name", null, null, null, null, null));
    }

    [Fact]
    public async Task UpdateAgentAsync_NullValues_OnlyUpdatesNonNullFields()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Original Name",
            Description = "Original Description",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, null, null, null, null, null, null);

        // Assert
        Assert.Equal("Original Name", result.Name);
        Assert.Equal("Original Description", result.Description);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAgentAsync_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Original Name",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, "", null, null, null, null, null));
        Assert.Contains("name cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_EmptySystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Agent Name",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, null, null, "   ", null, null, null));
        Assert.Contains("system prompt cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_InvalidLanguage_ThrowsArgumentException()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Agent Name",
            TutorLanguage = "pt-br",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, null, null, null, "fr", null, null));
        Assert.Contains("Invalid tutor language", exception.Message);
    }

    [Fact]
    public async Task UpdateAgentAsync_NonExistentAIModel_ThrowsArgumentException()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Agent Name",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _aiModelRepositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((AIModel?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAgentAsync(agentId, 1, "super_admin", 1, null, null, null, null, 999, null));
        Assert.Contains("AI Model with ID 999 not found", exception.Message);
    }

    #endregion

    #region DeleteAgentAsync Tests

    [Fact]
    public async Task DeleteAgentAsync_ExistingAgent_DeletesAgent()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAgentAsync(agentId, 1, "super_admin", 1);

        // Assert
        Assert.False(agent.IsActive); // Soft delete sets IsActive to false
        _agentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAgentAsync_NonExistentAgent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var agentId = 999;
        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DeleteAgentAsync(agentId, 1, "super_admin", 1));
    }

    #endregion

    #region CreateTokenAsync Tests

    [Fact]
    public async Task CreateTokenAsync_ValidRequest_CreatesAndReturnsToken()
    {
        // Arrange
        var agentId = 1;
        var currentUserId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = currentUserId,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _tokenRepositoryMock.Setup(r => r.AddAsync(It.IsAny<ProfessorAgentToken>()))
            .ReturnsAsync((ProfessorAgentToken token) => { token.Id = 1; return token; });

        // Act
        var result = await _service.CreateTokenAsync(
            agentId,
            currentUserId,
            "professor",
            "Test Token",
            "Description",
            true,
            DateTime.UtcNow.AddDays(365));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agentId, result.ProfessorAgentId);
        Assert.Equal(currentUserId, result.ProfessorId);
        Assert.Equal("Test Token", result.Name);
        Assert.NotNull(result.Token);
        _tokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ProfessorAgentToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTokenAsync_AgentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var agentId = 999;
        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateTokenAsync(agentId, 1, "professor", "Token", null, true, null));
    }

    [Fact]
    public async Task CreateTokenAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateTokenAsync(agentId, 2, "professor", "Token", null, true, null));
    }

    [Fact]
    public async Task CreateTokenAsync_SuperAdmin_CanCreateTokenForAnyAgent()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _tokenRepositoryMock.Setup(r => r.AddAsync(It.IsAny<ProfessorAgentToken>()))
            .ReturnsAsync((ProfessorAgentToken token) => { token.Id = 1; return token; });

        // Act
        var result = await _service.CreateTokenAsync(
            agentId,
            999,
            "super_admin",
            "Token",
            null,
            true,
            null);

        // Assert
        Assert.NotNull(result);
        _tokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ProfessorAgentToken>()), Times.Once);
    }

    #endregion

    #region GetTokensByAgentIdAsync Tests

    [Fact]
    public async Task GetTokensByAgentIdAsync_ExistingAgent_ReturnsTokens()
    {
        // Arrange
        var agentId = 1;
        var tokens = new List<ProfessorAgentToken>
        {
            new ProfessorAgentToken { Id = 1, ProfessorAgentId = agentId, Name = "Token 1", Token = "token-1" },
            new ProfessorAgentToken { Id = 2, ProfessorAgentId = agentId, Name = "Token 2", Token = "token-2" }
        };

        _tokenRepositoryMock.Setup(r => r.GetByProfessorAgentIdAsync(agentId))
            .ReturnsAsync(tokens);

        // Act
        var result = await _service.GetTokensByAgentIdAsync(agentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _tokenRepositoryMock.Verify(r => r.GetByProfessorAgentIdAsync(agentId), Times.Once);
    }

    #endregion

    #region GetProfessorAgentStatusAsync Tests

    [Fact]
    public async Task GetProfessorAgentStatusAsync_WithoutUniversityId_ReturnsAllProfessorsWithAgentStatus()
    {
        // Arrange
        var professors = new List<User>
        {
            new User
            {
                UserId = 1,
                Username = "john.doe",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@test.com",
                UserType = "professor",
                UniversityId = 1
            },
            new User
            {
                UserId = 2,
                Username = "jane.smith",
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane@test.com",
                UserType = "professor",
                UniversityId = 1
            }
        };

        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent
            {
                Id = 1,
                ProfessorId = 1,
                Name = "John's Agent",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _userRepositoryMock.Setup(r => r.GetByTypeAsync("professor"))
            .ReturnsAsync(professors);
        _agentRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(agents);

        // Act
        var result = await _service.GetProfessorAgentStatusAsync(null);

        // Assert
        Assert.NotNull(result);
        var statusList = result.ToList();
        Assert.Equal(2, statusList.Count);

        var profWithAgent = statusList.First(s => s.ProfessorId == 1);
        Assert.True(profWithAgent.HasAgent);
        Assert.Equal(1, profWithAgent.AgentId);
        Assert.Equal("John's Agent", profWithAgent.AgentName);
        Assert.True(profWithAgent.AgentIsActive);

        var profWithoutAgent = statusList.First(s => s.ProfessorId == 2);
        Assert.False(profWithoutAgent.HasAgent);
        Assert.Null(profWithoutAgent.AgentId);
    }

    [Fact]
    public async Task GetProfessorAgentStatusAsync_WithUniversityId_ReturnsFilteredProfessorsWithAgentStatus()
    {
        // Arrange
        var universityId = 1;
        var professors = new List<User>
        {
            new User
            {
                UserId = 1,
                Username = "john.doe",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@test.com",
                UserType = "professor",
                UniversityId = universityId
            }
        };

        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent
            {
                Id = 1,
                ProfessorId = 1,
                Name = "John's Agent",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _userRepositoryMock.Setup(r => r.GetByUniversityIdAsync(universityId))
            .ReturnsAsync(professors);
        _agentRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(agents);

        // Act
        var result = await _service.GetProfessorAgentStatusAsync(universityId);

        // Assert
        Assert.NotNull(result);
        var statusList = result.ToList();
        Assert.Single(statusList);

        var profStatus = statusList.First();
        Assert.True(profStatus.HasAgent);
        Assert.Equal(1, profStatus.AgentId);
        Assert.False(profStatus.AgentIsActive);
    }

    [Fact]
    public async Task GetProfessorAgentStatusAsync_NoProfessors_ReturnsEmptyList()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetByTypeAsync("professor"))
            .ReturnsAsync(new List<User>());
        _agentRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ProfessorAgent>());

        // Act
        var result = await _service.GetProfessorAgentStatusAsync(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region ActivateAgentAsync Tests

    [Fact]
    public async Task ActivateAgentAsync_ExistingAgent_SetsIsActiveToTrue()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = false,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);
        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ActivateAgentAsync(agentId, 1, "super_admin", 1);

        // Assert
        Assert.True(agent.IsActive);
        // UpdatedAt is handled by database triggers, not in code
        _agentRepositoryMock.Verify(r => r.UpdateAsync(agent), Times.Once);
    }

    [Fact]
    public async Task ActivateAgentAsync_NonExistentAgent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var agentId = 999;
        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.ActivateAgentAsync(agentId, 1, "super_admin", 1));
    }

    [Fact]
    public async Task ActivateAgentAsync_AlreadyActive_StillUpdates()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);
        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ActivateAgentAsync(agentId, 1, "super_admin", 1);

        // Assert
        Assert.True(agent.IsActive);
        _agentRepositoryMock.Verify(r => r.UpdateAsync(agent), Times.Once);
    }

    #endregion

    #region DeactivateAgentAsync Tests

    [Fact]
    public async Task DeactivateAgentAsync_ExistingAgent_SetsIsActiveToFalse()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);
        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeactivateAgentAsync(agentId, 1, "super_admin", 1);

        // Assert
        Assert.False(agent.IsActive);
        // UpdatedAt is handled by database triggers, not in code
        _agentRepositoryMock.Verify(r => r.UpdateAsync(agent), Times.Once);
    }

    [Fact]
    public async Task DeactivateAgentAsync_NonExistentAgent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var agentId = 999;
        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DeactivateAgentAsync(agentId, 1, "super_admin", 1));
    }

    [Fact]
    public async Task DeactivateAgentAsync_AlreadyInactive_StillUpdates()
    {
        // Arrange
        var agentId = 1;
        var agent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = false,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);
        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeactivateAgentAsync(agentId, 1, "super_admin", 1);

        // Assert
        Assert.False(agent.IsActive);
        _agentRepositoryMock.Verify(r => r.UpdateAsync(agent), Times.Once);
    }

    #endregion
}
