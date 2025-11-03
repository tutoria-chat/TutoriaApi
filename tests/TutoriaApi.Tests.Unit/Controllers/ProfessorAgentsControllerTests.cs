using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using CoreDTOs = TutoriaApi.Core.DTOs;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Tests.Unit.Controllers;

public class ProfessorAgentsControllerTests
{
    private readonly Mock<IProfessorAgentService> _serviceMock;
    private readonly Mock<ILogger<ProfessorAgentsController>> _loggerMock;
    private readonly ProfessorAgentsController _controller;
    private readonly User _testProfessor;

    public ProfessorAgentsControllerTests()
    {
        _serviceMock = new Mock<IProfessorAgentService>();
        _loggerMock = new Mock<ILogger<ProfessorAgentsController>>();

        _controller = new ProfessorAgentsController(
            _serviceMock.Object,
            _loggerMock.Object);

        // Setup test professor
        _testProfessor = new User
        {
            UserId = 1,
            Username = "testprofessor",
            Email = "professor@test.com",
            FirstName = "Test",
            LastName = "Professor",
            UserType = "professor",
            UniversityId = 1,
            IsActive = true
        };

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testProfessor.UserId.ToString()),
            new Claim(ClaimTypes.Name, _testProfessor.Username),
            new Claim(ClaimTypes.Email, _testProfessor.Email),
            new Claim(ClaimTypes.Role, _testProfessor.UserType)
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #region GetMyAgent Tests

    [Fact]
    public async Task GetMyAgent_ExistingAgent_ReturnsOkWithDto()
    {
        // Arrange
        var agent = new ProfessorAgent
        {
            Id = 1,
            ProfessorId = _testProfessor.UserId,
            UniversityId = 1,
            Name = "My Agent",
            Description = "Description",
            TutorLanguage = "en",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var tokens = new List<ProfessorAgentToken>
        {
            new ProfessorAgentToken
            {
                Id = 1,
                ProfessorAgentId = agent.Id,
                ProfessorId = agent.ProfessorId,
                Name = "Token 1",
                Token = "token123",
                AllowChat = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _serviceMock.Setup(s => s.GetByProfessorIdAsync(_testProfessor.UserId))
            .ReturnsAsync(agent);

        _serviceMock.Setup(s => s.GetTokensByAgentIdAsync(agent.Id))
            .ReturnsAsync(tokens);

        // Act
        var result = await _controller.GetMyAgent();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProfessorAgentDetailDto>(okResult.Value);
        Assert.Equal(agent.Id, dto.Id);
        Assert.Equal(agent.Name, dto.Name);
        Assert.Single(dto.Tokens);
    }

    [Fact]
    public async Task GetMyAgent_NoAgent_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetByProfessorIdAsync(_testProfessor.UserId))
            .ReturnsAsync((ProfessorAgent?)null);

        // Act
        var result = await _controller.GetMyAgent();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMyAgent_ServiceThrowsException_Returns500()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetByProfessorIdAsync(_testProfessor.UserId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetMyAgent();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    #endregion

    #region GetAllAgents Tests

    [Fact]
    public async Task GetAllAgents_NoUniversityFilter_ReturnsActiveAgents()
    {
        // Arrange
        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent { Id = 1, Name = "Agent 1", IsActive = true },
            new ProfessorAgent { Id = 2, Name = "Agent 2", IsActive = true }
        };

        _serviceMock.Setup(s => s.GetAllAgentsAsync(null))
            .ReturnsAsync(agents);

        // Act
        var result = await _controller.GetAllAgents();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<ProfessorAgentListDto>>(okResult.Value);
        Assert.Equal(2, dtos.Count());
    }

    [Fact]
    public async Task GetAllAgents_WithUniversityFilter_ReturnsFilteredAgents()
    {
        // Arrange
        var universityId = 1;
        var agents = new List<ProfessorAgent>
        {
            new ProfessorAgent { Id = 1, UniversityId = universityId, Name = "Agent 1", IsActive = true }
        };

        _serviceMock.Setup(s => s.GetAllAgentsAsync(universityId))
            .ReturnsAsync(agents);

        // Act
        var result = await _controller.GetAllAgents(universityId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<ProfessorAgentListDto>>(okResult.Value);
        Assert.Single(dtos);
    }

    #endregion

    #region CreateAgent Tests

    [Fact]
    public async Task CreateAgent_ValidRequest_ReturnsCreatedWithDto()
    {
        // Arrange
        var request = new ProfessorAgentCreateRequest
        {
            ProfessorId = 2,
            Name = "New AI Agent",
            Description = "Description",
            TutorLanguage = "en",
            AIModelId = 1
        };

        var createdAgent = new ProfessorAgent
        {
            Id = 1,
            ProfessorId = request.ProfessorId,
            UniversityId = 1,
            Name = request.Name,
            Description = request.Description,
            TutorLanguage = request.TutorLanguage,
            AIModelId = request.AIModelId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _serviceMock.Setup(s => s.CreateAgentAsync(
            request.ProfessorId,
            request.Name,
            request.Description,
            request.SystemPrompt,
            request.TutorLanguage,
            request.AIModelId))
            .ReturnsAsync(createdAgent);

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ProfessorAgentDetailDto>(createdResult.Value);
        Assert.Equal(1, dto.Id);
        Assert.Equal("New AI Agent", dto.Name);
    }

    [Fact]
    public async Task CreateAgent_ProfessorNotFound_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProfessorAgentCreateRequest
        {
            ProfessorId = 999,
            Name = "New AI Agent",
            TutorLanguage = "en"
        };

        _serviceMock.Setup(s => s.CreateAgentAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("Invalid professor ID"));

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateAgent_AgentAlreadyExists_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProfessorAgentCreateRequest
        {
            ProfessorId = 1,
            Name = "New AI Agent",
            TutorLanguage = "en"
        };

        _serviceMock.Setup(s => s.CreateAgentAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("Professor already has an agent"));

        // Act
        var result = await _controller.CreateAgent(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    #endregion

    #region UpdateAgent Tests

    [Fact]
    public async Task UpdateAgent_ValidRequest_ReturnsOkWithDto()
    {
        // Arrange
        var agentId = 1;
        var request = new ProfessorAgentUpdateRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            TutorLanguage = "es"
        };

        var updatedAgent = new ProfessorAgent
        {
            Id = agentId,
            ProfessorId = 1,
            UniversityId = 1,
            Name = request.Name,
            Description = request.Description,
            TutorLanguage = request.TutorLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _serviceMock.Setup(s => s.UpdateAgentAsync(
            agentId,
            request.Name,
            request.Description,
            request.SystemPrompt,
            request.TutorLanguage,
            request.AIModelId,
            request.IsActive))
            .ReturnsAsync(updatedAgent);

        // Act
        var result = await _controller.UpdateAgent(agentId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProfessorAgentDetailDto>(okResult.Value);
        Assert.Equal("Updated Name", dto.Name);
    }

    [Fact]
    public async Task UpdateAgent_NonExistentAgent_ReturnsNotFound()
    {
        // Arrange
        var agentId = 999;
        var request = new ProfessorAgentUpdateRequest { Name = "Updated Name" };

        _serviceMock.Setup(s => s.UpdateAgentAsync(
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<int?>(),
            It.IsAny<bool?>()))
            .ThrowsAsync(new KeyNotFoundException("Professor agent not found"));

        // Act
        var result = await _controller.UpdateAgent(agentId, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    #endregion

    #region DeleteAgent Tests

    [Fact]
    public async Task DeleteAgent_ExistingAgent_ReturnsNoContent()
    {
        // Arrange
        var agentId = 1;
        _serviceMock.Setup(s => s.DeleteAgentAsync(agentId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteAgent(agentId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAgent_NonExistentAgent_ReturnsNotFound()
    {
        // Arrange
        var agentId = 999;
        _serviceMock.Setup(s => s.DeleteAgentAsync(agentId))
            .ThrowsAsync(new KeyNotFoundException("Professor agent not found"));

        // Act
        var result = await _controller.DeleteAgent(agentId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region CreateToken Tests

    [Fact]
    public async Task CreateToken_ValidRequest_ReturnsOkWithDto()
    {
        // Arrange
        var agentId = 1;
        var request = new ProfessorAgentTokenCreateRequest
        {
            Name = "Widget Token",
            Description = "Token for widget access",
            AllowChat = true,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };

        var createdToken = new ProfessorAgentToken
        {
            Id = 1,
            ProfessorAgentId = agentId,
            ProfessorId = _testProfessor.UserId,
            Name = request.Name,
            Description = request.Description,
            Token = "generated-token-1234567890abcdef",
            AllowChat = request.AllowChat,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _serviceMock.Setup(s => s.CreateTokenAsync(
            agentId,
            _testProfessor.UserId,
            _testProfessor.UserType,
            request.Name,
            request.Description,
            request.AllowChat,
            request.ExpiresAt))
            .ReturnsAsync(createdToken);

        // Act
        var result = await _controller.CreateToken(agentId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProfessorAgentTokenDetailDto>(okResult.Value);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Widget Token", dto.Name);
        Assert.NotNull(dto.Token);
    }

    [Fact]
    public async Task CreateToken_AgentNotFound_ReturnsNotFound()
    {
        // Arrange
        var agentId = 999;
        var request = new ProfessorAgentTokenCreateRequest
        {
            Name = "Token",
            AllowChat = true
        };

        _serviceMock.Setup(s => s.CreateTokenAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<DateTime?>()))
            .ThrowsAsync(new KeyNotFoundException("Professor agent not found"));

        // Act
        var result = await _controller.CreateToken(agentId, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateToken_UnauthorizedUser_ReturnsForbid()
    {
        // Arrange
        var agentId = 1;
        var request = new ProfessorAgentTokenCreateRequest
        {
            Name = "Token",
            AllowChat = true
        };

        _serviceMock.Setup(s => s.CreateTokenAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<DateTime?>()))
            .ThrowsAsync(new UnauthorizedAccessException("User is not authorized"));

        // Act
        var result = await _controller.CreateToken(agentId, request);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    #endregion

    #region GetProfessorAgentStatus Tests

    [Fact]
    public async Task GetProfessorAgentStatus_NoUniversityIdProvided_ReturnsAllProfessorStatuses()
    {
        // Arrange
        var statuses = new List<CoreDTOs.ProfessorAgentStatusDto>
        {
            new CoreDTOs.ProfessorAgentStatusDto
            {
                ProfessorId = 1,
                ProfessorName = "John Doe",
                ProfessorEmail = "john@test.com",
                HasAgent = true,
                AgentId = 1,
                AgentName = "John's Agent",
                AgentIsActive = true,
                AgentCreatedAt = DateTime.UtcNow
            },
            new CoreDTOs.ProfessorAgentStatusDto
            {
                ProfessorId = 2,
                ProfessorName = "Jane Smith",
                ProfessorEmail = "jane@test.com",
                HasAgent = false
            }
        };

        _serviceMock.Setup(s => s.GetProfessorAgentStatusAsync(null))
            .Returns(Task.FromResult<IEnumerable<CoreDTOs.ProfessorAgentStatusDto>>(statuses));

        // Act
        var result = await _controller.GetProfessorAgentStatus(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<CoreDTOs.ProfessorAgentStatusDto>>(okResult.Value);
        Assert.Equal(2, dtos.Count());
    }

    [Fact]
    public async Task GetProfessorAgentStatus_WithUniversityId_ReturnsFilteredStatuses()
    {
        // Arrange
        var universityId = 1;
        var statuses = new List<CoreDTOs.ProfessorAgentStatusDto>
        {
            new CoreDTOs.ProfessorAgentStatusDto
            {
                ProfessorId = 1,
                ProfessorName = "John Doe",
                ProfessorEmail = "john@test.com",
                HasAgent = true,
                AgentId = 1,
                AgentName = "John's Agent",
                AgentIsActive = true,
                AgentCreatedAt = DateTime.UtcNow
            }
        };

        _serviceMock.Setup(s => s.GetProfessorAgentStatusAsync(universityId))
            .Returns(Task.FromResult<IEnumerable<CoreDTOs.ProfessorAgentStatusDto>>(statuses));

        // Act
        var result = await _controller.GetProfessorAgentStatus(universityId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<CoreDTOs.ProfessorAgentStatusDto>>(okResult.Value);
        Assert.Single(dtos);
    }

    [Fact]
    public async Task GetProfessorAgentStatus_ServiceThrowsException_Returns500()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetProfessorAgentStatusAsync(It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetProfessorAgentStatus(null);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    #endregion

    #region DeactivateAgent Tests

    [Fact]
    public async Task DeactivateAgent_ExistingAgent_ReturnsNoContent()
    {
        // Arrange
        var agentId = 1;
        _serviceMock.Setup(s => s.DeactivateAgentAsync(agentId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeactivateAgent(agentId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _serviceMock.Verify(s => s.DeactivateAgentAsync(agentId), Times.Once);
    }

    [Fact]
    public async Task DeactivateAgent_NonExistentAgent_ReturnsNotFound()
    {
        // Arrange
        var agentId = 999;
        _serviceMock.Setup(s => s.DeactivateAgentAsync(agentId))
            .ThrowsAsync(new KeyNotFoundException("Professor agent not found"));

        // Act
        var result = await _controller.DeactivateAgent(agentId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateAgent_ServiceThrowsException_Returns500()
    {
        // Arrange
        var agentId = 1;
        _serviceMock.Setup(s => s.DeactivateAgentAsync(agentId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.DeactivateAgent(agentId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    #endregion

    #region ActivateAgent Tests

    [Fact]
    public async Task ActivateAgent_ExistingAgent_ReturnsNoContent()
    {
        // Arrange
        var agentId = 1;
        _serviceMock.Setup(s => s.ActivateAgentAsync(agentId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ActivateAgent(agentId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _serviceMock.Verify(s => s.ActivateAgentAsync(agentId), Times.Once);
    }

    [Fact]
    public async Task ActivateAgent_NonExistentAgent_ReturnsNotFound()
    {
        // Arrange
        var agentId = 999;
        _serviceMock.Setup(s => s.ActivateAgentAsync(agentId))
            .ThrowsAsync(new KeyNotFoundException("Professor agent not found"));

        // Act
        var result = await _controller.ActivateAgent(agentId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ActivateAgent_ServiceThrowsException_Returns500()
    {
        // Arrange
        var agentId = 1;
        _serviceMock.Setup(s => s.ActivateAgentAsync(agentId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.ActivateAgent(agentId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    #endregion
}
