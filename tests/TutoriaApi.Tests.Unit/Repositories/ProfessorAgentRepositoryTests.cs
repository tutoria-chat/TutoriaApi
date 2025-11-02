using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;

namespace TutoriaApi.Tests.Unit.Repositories;

public class ProfessorAgentRepositoryTests
{
    private readonly TutoriaDbContext _context;
    private readonly ProfessorAgentRepository _repository;
    private readonly List<ProfessorAgent> _testData;
    private readonly User _testProfessor;
    private readonly University _testUniversity;
    private readonly AIModel _testAIModel;

    public ProfessorAgentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TutoriaDbContext(options);
        _repository = new ProfessorAgentRepository(_context);

        _testProfessor = new User
        {
            UserId = 1,
            Username = "testprof",
            Email = "prof@test.com",
            FirstName = "Test",
            LastName = "Professor",
            UserType = "professor",
            UniversityId = 1
        };

        _testUniversity = new University
        {
            Id = 1,
            Name = "Test University",
            Code = "TEST"
        };

        _testAIModel = new AIModel
        {
            Id = 1,
            ModelName = "gpt-4",
            DisplayName = "GPT-4",
            Provider = "openai"
        };

        _testData = new List<ProfessorAgent>
        {
            new ProfessorAgent
            {
                Id = 1,
                ProfessorId = 1,
                UniversityId = 1,
                Name = "Active Agent 1",
                IsActive = true,
                AIModelId = 1,
                Professor = _testProfessor,
                University = _testUniversity,
                AIModel = _testAIModel,
                ProfessorAgentTokens = new List<ProfessorAgentToken>()
            },
            new ProfessorAgent
            {
                Id = 2,
                ProfessorId = 1,
                UniversityId = 1,
                Name = "Inactive Agent",
                IsActive = false,
                AIModelId = 1,
                Professor = _testProfessor,
                University = _testUniversity,
                AIModel = _testAIModel,
                ProfessorAgentTokens = new List<ProfessorAgentToken>()
            },
            new ProfessorAgent
            {
                Id = 3,
                ProfessorId = 2,
                UniversityId = 1,
                Name = "Active Agent 2",
                IsActive = true,
                AIModelId = 1,
                Professor = new User
                {
                    UserId = 2,
                    Username = "prof2",
                    Email = "prof2@test.com",
                    FirstName = "Test2",
                    LastName = "Professor2",
                    UserType = "professor"
                },
                University = _testUniversity,
                AIModel = _testAIModel,
                ProfessorAgentTokens = new List<ProfessorAgentToken>()
            },
            new ProfessorAgent
            {
                Id = 4,
                ProfessorId = 3,
                UniversityId = 2,
                Name = "Different University Agent",
                IsActive = true,
                AIModelId = 1,
                Professor = new User
                {
                    UserId = 3,
                    Username = "prof3",
                    Email = "prof3@test.com",
                    FirstName = "Test3",
                    LastName = "Professor3",
                    UserType = "professor"
                },
                University = new University { Id = 2, Name = "Other Uni", Code = "OTHER" },
                AIModel = _testAIModel,
                ProfessorAgentTokens = new List<ProfessorAgentToken>()
            }
        };

        // Seed the in-memory database with test data
        _context.ProfessorAgents.AddRange(_testData);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByProfessorIdAsync_ActiveAgentExists_ReturnsAgent()
    {
        // Act
        var result = await _repository.GetByProfessorIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Active Agent 1", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetByProfessorIdAsync_OnlyInactiveAgentExists_ReturnsNull()
    {
        // Arrange - Clear existing data and add only inactive agent
        _context.ProfessorAgents.RemoveRange(_context.ProfessorAgents);
        _context.ProfessorAgents.Add(new ProfessorAgent
        {
            Id = 5,
            ProfessorId = 5,
            UniversityId = 1,
            Name = "Inactive Agent",
            IsActive = false
        });
        _context.SaveChanges();

        // Act
        var result = await _repository.GetByProfessorIdAsync(5);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByProfessorIdAsync_NoAgentExists_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByProfessorIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWithDetailsAsync_ExistingAgent_ReturnsAgentWithIncludes()
    {
        // Act
        var result = await _repository.GetWithDetailsAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.NotNull(result.Professor);
        Assert.NotNull(result.University);
        Assert.NotNull(result.AIModel);
        Assert.NotNull(result.ProfessorAgentTokens);
    }

    [Fact]
    public async Task GetWithDetailsAsync_NonExistentAgent_ReturnsNull()
    {
        // Act
        var result = await _repository.GetWithDetailsAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUniversityIdAsync_ExistingUniversity_ReturnsAgents()
    {
        // Act
        var result = await _repository.GetByUniversityIdAsync(1);

        // Assert
        Assert.NotNull(result);
        var agents = result.ToList();
        Assert.Equal(3, agents.Count); // 2 active + 1 inactive for university 1
        Assert.All(agents, a => Assert.Equal(1, a.UniversityId));
    }

    [Fact]
    public async Task GetByUniversityIdAsync_NonExistentUniversity_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByUniversityIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExistsByProfessorIdAsync_ActiveAgentExists_ReturnsTrue()
    {
        // Act
        var result = await _repository.ExistsByProfessorIdAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByProfessorIdAsync_OnlyInactiveExists_ReturnsFalse()
    {
        // Arrange - Clear and add only inactive agent
        _context.ProfessorAgents.RemoveRange(_context.ProfessorAgents);
        _context.ProfessorAgents.Add(new ProfessorAgent
        {
            Id = 5,
            ProfessorId = 5,
            UniversityId = 1,
            Name = "Inactive Agent",
            IsActive = false
        });
        _context.SaveChanges();

        // Act
        var result = await _repository.ExistsByProfessorIdAsync(5);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByProfessorIdAsync_NoAgentExists_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByProfessorIdAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetActiveAgentsAsync_ReturnsOnlyActiveAgents()
    {
        // Act
        var result = await _repository.GetActiveAgentsAsync();

        // Assert
        Assert.NotNull(result);
        var agents = result.ToList();
        Assert.Equal(3, agents.Count); // Only active agents
        Assert.All(agents, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task GetActiveAgentsAsync_NoActiveAgents_ReturnsEmptyList()
    {
        // Arrange - Clear and add only inactive agents
        _context.ProfessorAgents.RemoveRange(_context.ProfessorAgents);
        _context.ProfessorAgents.Add(new ProfessorAgent
        {
            Id = 1,
            ProfessorId = 1,
            UniversityId = 1,
            Name = "Inactive Agent",
            IsActive = false
        });
        _context.SaveChanges();

        // Act
        var result = await _repository.GetActiveAgentsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
