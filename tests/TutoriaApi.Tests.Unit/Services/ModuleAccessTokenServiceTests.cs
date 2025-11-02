using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Helpers;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class ModuleAccessTokenServiceTests
{
    private readonly Mock<IModuleAccessTokenRepository> _tokenRepositoryMock;
    private readonly Mock<IModuleRepository> _moduleRepositoryMock;
    private readonly AccessControlHelper _accessControl;
    private readonly ModuleAccessTokenService _service;

    public ModuleAccessTokenServiceTests()
    {
        _tokenRepositoryMock = new Mock<IModuleAccessTokenRepository>();
        _moduleRepositoryMock = new Mock<IModuleRepository>();

        // Create in-memory database for AccessControlHelper
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new TutoriaDbContext(options);
        var logger = Mock.Of<ILogger<AccessControlHelper>>();
        _accessControl = new AccessControlHelper(context, logger);

        _service = new ModuleAccessTokenService(
            _tokenRepositoryMock.Object,
            _moduleRepositoryMock.Object,
            _accessControl);
    }

    private Module CreateTestModule(string name = "Test Module")
    {
        return new Module
        {
            Id = 1,
            Name = name,
            Code = "MOD1",
            SystemPrompt = "Test prompt",
            Course = new Course
            {
                Id = 1,
                Name = "Test Course",
                Code = "CS101",
                University = new University
                {
                    Id = 1,
                    Name = "Test University",
                    Code = "TU"
                }
            }
        };
    }

    private User CreateTestUser(int id = 1, string userType = "professor")
    {
        return new User
        {
            UserId = id,
            Username = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            UserType = userType
        };
    }

    private ModuleAccessToken CreateTestToken(int id = 1)
    {
        return new ModuleAccessToken
        {
            Id = id,
            Token = "test-token",
            Name = "Test Token",
            ModuleId = 1,
            Module = CreateTestModule(),
            CreatedBy = CreateTestUser()
        };
    }

    [Fact]
    public async Task GetWithDetailsAsync_ExistingToken_ReturnsViewModel()
    {
        // Arrange
        var token = CreateTestToken();
        _tokenRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(token);

        // Act
        var result = await _service.GetWithDetailsAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(token, result.Token);
        Assert.Equal("Test Module", result.ModuleName);
    }

    [Fact]
    public async Task GetWithDetailsAsync_NonExistentToken_ReturnsNull()
    {
        // Arrange
        _tokenRepositoryMock.Setup(r => r.GetWithDetailsAsync(999)).ReturnsAsync((ModuleAccessToken?)null);

        // Act
        var result = await _service.GetWithDetailsAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPagedAsync_SuperAdmin_ReturnsAllTokens()
    {
        // Arrange
        var superAdmin = CreateTestUser(userType: "super_admin");
        var tokens = new List<ModuleAccessToken> { CreateTestToken(1), CreateTestToken(2) };
        _tokenRepositoryMock.Setup(r => r.SearchAsync(null, null, null, 1, 10, null))
            .ReturnsAsync((tokens, 2));

        // Act
        var (items, total) = await _service.GetPagedAsync(null, null, null, 1, 10, superAdmin);

        // Assert
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task CreateAsync_ValidData_GeneratesSecureTokenAndCreatesRecord()
    {
        // Arrange
        var module = CreateTestModule();
        var user = CreateTestUser();
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(module);
        _tokenRepositoryMock.Setup(r => r.AddAsync(It.IsAny<ModuleAccessToken>())).ReturnsAsync((ModuleAccessToken t) => t);

        // Act
        var result = await _service.CreateAsync(1, "Test Token", "Desc", true, true, 30, user);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Token.Token.Length > 40);
        Assert.Equal("Test Token", result.Token.Name);
        Assert.True(result.Token.IsActive);
    }

    [Fact]
    public async Task CreateAsync_NonExistentModule_ThrowsKeyNotFoundException()
    {
        // Arrange
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(999)).ReturnsAsync((Module?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateAsync(999, "Test", null, true, true, null, CreateTestUser()));
    }

    [Fact]
    public async Task UpdateAsync_ExistingToken_UpdatesFields()
    {
        // Arrange
        var token = CreateTestToken();
        token.Name = "Old Name";
        _tokenRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(token);
        _tokenRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ModuleAccessToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateAsync(1, "New Name", "New Desc", false, true, true);

        // Assert
        Assert.Equal("New Name", result.Token.Name);
        Assert.False(result.Token.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentToken_ThrowsKeyNotFoundException()
    {
        // Arrange
        _tokenRepositoryMock.Setup(r => r.GetWithDetailsAsync(999)).ReturnsAsync((ModuleAccessToken?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateAsync(999, "New Name", null, null, null, null));
    }

    [Fact]
    public async Task DeleteAsync_ExistingToken_DeletesToken()
    {
        // Arrange
        var token = CreateTestToken();
        _tokenRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(token);
        _tokenRepositoryMock.Setup(r => r.DeleteAsync(token)).Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAsync(1);

        // Assert
        _tokenRepositoryMock.Verify(r => r.DeleteAsync(token), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentToken_ThrowsKeyNotFoundException()
    {
        // Arrange
        _tokenRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((ModuleAccessToken?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync(999));
    }
}
