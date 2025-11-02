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
    private readonly ProfessorAgentService _service;

    public ProfessorAgentServiceTests()
    {
        _agentRepositoryMock = new Mock<IProfessorAgentRepository>();
        _tokenRepositoryMock = new Mock<IProfessorAgentTokenRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _service = new ProfessorAgentService(
            _agentRepositoryMock.Object,
            _tokenRepositoryMock.Object,
            _userRepositoryMock.Object);
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

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdAsync(professorId))
            .ReturnsAsync((ProfessorAgent?)null);

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

        _userRepositoryMock.Setup(r => r.GetByIdAsync(professorId))
            .ReturnsAsync(professor);

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdAsync(professorId))
            .ReturnsAsync((ProfessorAgent?)null);

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

        _agentRepositoryMock.Setup(r => r.GetByProfessorIdAsync(professorId))
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

        _agentRepositoryMock.Setup(r => r.GetByIdAsync(agentId))
            .ReturnsAsync(agent);

        _agentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ProfessorAgent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAgentAsync(
            agentId,
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
            () => _service.UpdateAgentAsync(agentId, "New Name", null, null, null, null, null));
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
        var result = await _service.UpdateAgentAsync(agentId, null, null, null, null, null, null);

        // Assert
        Assert.Equal("Original Name", result.Name);
        Assert.Equal("Original Description", result.Description);
        Assert.True(result.IsActive);
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

        _agentRepositoryMock.Setup(r => r.DeleteAsync(agent))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAgentAsync(agentId);

        // Assert
        _agentRepositoryMock.Verify(r => r.DeleteAsync(agent), Times.Once);
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
            () => _service.DeleteAgentAsync(agentId));
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
}
