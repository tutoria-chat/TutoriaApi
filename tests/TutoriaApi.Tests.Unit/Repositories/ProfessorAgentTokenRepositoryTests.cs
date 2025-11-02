using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;

namespace TutoriaApi.Tests.Unit.Repositories;

public class ProfessorAgentTokenRepositoryTests
{
    private readonly TutoriaDbContext _context;
    private readonly ProfessorAgentTokenRepository _repository;
    private readonly List<ProfessorAgentToken> _testData;
    private readonly ProfessorAgent _testAgent;

    public ProfessorAgentTokenRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TutoriaDbContext(options);
        _repository = new ProfessorAgentTokenRepository(_context);

        _testAgent = new ProfessorAgent
        {
            Id = 1,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Test Agent",
            IsActive = true,
            Professor = new User
            {
                UserId = 1,
                Username = "testprof",
                Email = "testprof@test.com",
                FirstName = "Test",
                LastName = "Professor",
                UserType = "professor"
            },
            University = new University { Id = 1, Name = "Test Uni", Code = "TEST" },
            AIModel = new AIModel { Id = 1, ModelName = "gpt-4", DisplayName = "GPT-4", Provider = "openai" }
        };

        _testData = new List<ProfessorAgentToken>
        {
            new ProfessorAgentToken
            {
                Id = 1,
                Token = "valid-token-123",
                ProfessorAgentId = 1,
                ProfessorId = 1,
                Name = "Token 1",
                AllowChat = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ProfessorAgent = _testAgent
            },
            new ProfessorAgentToken
            {
                Id = 2,
                Token = "expired-token-456",
                ProfessorAgentId = 1,
                ProfessorId = 1,
                Name = "Token 2",
                AllowChat = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ProfessorAgent = _testAgent
            },
            new ProfessorAgentToken
            {
                Id = 3,
                Token = "no-chat-token-789",
                ProfessorAgentId = 1,
                ProfessorId = 1,
                Name = "Token 3",
                AllowChat = false, // Chat not allowed
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ProfessorAgent = _testAgent
            },
            new ProfessorAgentToken
            {
                Id = 4,
                Token = "no-expiry-token",
                ProfessorAgentId = 1,
                ProfessorId = 1,
                Name = "Token 4",
                AllowChat = true,
                ExpiresAt = null, // No expiry
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ProfessorAgent = _testAgent
            },
            new ProfessorAgentToken
            {
                Id = 5,
                Token = "different-agent-token",
                ProfessorAgentId = 2,
                ProfessorId = 2,
                Name = "Token 5",
                AllowChat = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ProfessorAgent = new ProfessorAgent
                {
                    Id = 2,
                    ProfessorId = 2,
                    UniversityId = 1,
                    Name = "Agent 2",
                    IsActive = true
                }
            }
        };

        // Seed the in-memory database with test data
        _context.ProfessorAgentTokens.AddRange(_testData);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByTokenAsync_ExistingToken_ReturnsToken()
    {
        // Act
        var result = await _repository.GetByTokenAsync("valid-token-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("valid-token-123", result.Token);
        Assert.Equal("Token 1", result.Name);
    }

    [Fact]
    public async Task GetByTokenAsync_NonExistentToken_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByTokenAsync("non-existent-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByProfessorAgentIdAsync_ExistingAgent_ReturnsTokensOrderedByCreatedAt()
    {
        // Act
        var result = await _repository.GetByProfessorAgentIdAsync(1);

        // Assert
        Assert.NotNull(result);
        var tokens = result.ToList();
        Assert.Equal(4, tokens.Count); // 4 tokens for agent 1

        // Verify order (most recent first)
        Assert.Equal("valid-token-123", tokens[0].Token); // Created -1 day
        Assert.Equal("no-chat-token-789", tokens[1].Token); // Created -2 days
        Assert.Equal("no-expiry-token", tokens[2].Token); // Created -3 days
        Assert.Equal("expired-token-456", tokens[3].Token); // Created -5 days
    }

    [Fact]
    public async Task GetByProfessorAgentIdAsync_NonExistentAgent_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByProfessorAgentIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByProfessorIdAsync_ExistingProfessor_ReturnsTokensOrderedByCreatedAt()
    {
        // Act
        var result = await _repository.GetByProfessorIdAsync(1);

        // Assert
        Assert.NotNull(result);
        var tokens = result.ToList();
        Assert.Equal(4, tokens.Count); // 4 tokens for professor 1
        Assert.All(tokens, t => Assert.Equal(1, t.ProfessorId));

        // Verify order
        Assert.Equal("valid-token-123", tokens[0].Token);
    }

    [Fact]
    public async Task GetByProfessorIdAsync_NonExistentProfessor_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByProfessorIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_ValidToken_ReturnsTrue()
    {
        // Act
        var result = await _repository.IsTokenValidAsync("valid-token-123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_ExpiredToken_ReturnsFalse()
    {
        // Act
        var result = await _repository.IsTokenValidAsync("expired-token-456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_ChatNotAllowed_ReturnsFalse()
    {
        // Act
        var result = await _repository.IsTokenValidAsync("no-chat-token-789");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_NoExpiry_ReturnsTrue()
    {
        // Act
        var result = await _repository.IsTokenValidAsync("no-expiry-token");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsTokenValidAsync_NonExistentToken_ReturnsFalse()
    {
        // Act
        var result = await _repository.IsTokenValidAsync("non-existent-token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetByTokenWithDetailsAsync_ExistingToken_ReturnsTokenWithIncludes()
    {
        // Act
        var result = await _repository.GetByTokenWithDetailsAsync("valid-token-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("valid-token-123", result.Token);
        Assert.NotNull(result.ProfessorAgent);
        Assert.NotNull(result.ProfessorAgent.Professor);
        Assert.NotNull(result.ProfessorAgent.University);
        Assert.NotNull(result.ProfessorAgent.AIModel);
    }

    [Fact]
    public async Task GetByTokenWithDetailsAsync_NonExistentToken_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByTokenWithDetailsAsync("non-existent-token");

        // Assert
        Assert.Null(result);
    }
}
