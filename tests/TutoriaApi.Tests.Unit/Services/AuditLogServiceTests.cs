using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _repositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<AuditLogService>> _loggerMock;
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _repositoryMock = new Mock<IAuditLogRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<AuditLogService>>();
        _service = new AuditLogService(_repositoryMock.Object, _userRepositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPagedAsync_SuperAdmin_ReturnsAllLogs()
    {
        // Arrange
        var superAdmin = CreateSuperAdminUser();
        var logs = new List<AuditLog>
        {
            CreateAuditLog(1, "Create", "University"),
            CreateAuditLog(2, "Update", "Course")
        };

        _repositoryMock.Setup(r => r.SearchAsync(
            null, null, null, null, null, null, null, 1, 10))
            .ReturnsAsync((logs, 2));

        // Act
        var (result, total) = await _service.GetPagedAsync(
            null, null, null, null, null, null, null, 1, 10, superAdmin);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task GetPagedAsync_Manager_FiltersByUniversityId()
    {
        // Arrange
        var manager = CreateManagerUser(universityId: 1);
        var logs = new List<AuditLog>
        {
            CreateAuditLog(1, "Create", "Course", universityId: 1)
        };

        _repositoryMock.Setup(r => r.SearchAsync(
            1, null, null, null, null, null, null, 1, 10))
            .ReturnsAsync((logs, 1));

        // Act
        var (result, total) = await _service.GetPagedAsync(
            null, null, null, null, null, null, null, 1, 10, manager);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].UniversityId);
        _repositoryMock.Verify(r => r.SearchAsync(
            1, null, null, null, null, null, null, 1, 10), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_NonAdminUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var professor = CreateProfessorUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetPagedAsync(null, null, null, null, null, null, null, 1, 10, professor));
    }

    [Fact]
    public async Task ExportToCsvAsync_SuperAdmin_ReturnsAllLogsAsCsv()
    {
        // Arrange
        var superAdmin = CreateSuperAdminUser();
        var logs = new List<AuditLog>
        {
            CreateAuditLog(1, "Create", "University", entityName: "Test University"),
            CreateAuditLog(2, "Update", "Course", entityName: "Test Course", changes: "{\"Name\":{\"Old\":\"Old Name\",\"New\":\"New Name\"}}")
        };

        _repositoryMock.Setup(r => r.GetAllForExportAsync(
            null, null, null, null, null, null))
            .ReturnsAsync(logs);

        // Act
        var csvBytes = await _service.ExportToCsvAsync(
            null, null, null, null, null, null, superAdmin);

        // Assert
        Assert.NotNull(csvBytes);
        Assert.True(csvBytes.Length > 0);

        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);
        Assert.Contains("Timestamp,User,Action,Entity Type,Entity,Changes", csv);
        Assert.Contains("Create", csv);
        Assert.Contains("Update", csv);
        Assert.Contains("Test University", csv);
        Assert.Contains("Test Course", csv);
    }

    [Fact]
    public async Task ExportToCsvAsync_Manager_FiltersByUniversityId()
    {
        // Arrange
        var manager = CreateManagerUser(universityId: 1);
        var logs = new List<AuditLog>
        {
            CreateAuditLog(1, "Create", "Course", universityId: 1)
        };

        _repositoryMock.Setup(r => r.GetAllForExportAsync(
            1, null, null, null, null, null))
            .ReturnsAsync(logs);

        // Act
        var csvBytes = await _service.ExportToCsvAsync(
            null, null, null, null, null, null, manager);

        // Assert
        Assert.NotNull(csvBytes);
        _repositoryMock.Verify(r => r.GetAllForExportAsync(
            1, null, null, null, null, null), Times.Once);
    }

    [Fact]
    public async Task LogAsync_ValidData_CreatesAuditLog()
    {
        // Arrange
        var changes = new Dictionary<string, (object? OldValue, object? NewValue)>
        {
            ["Name"] = ("Old Name", "New Name"),
            ["Description"] = ("Old Desc", "New Desc")
        };

        AuditLog? capturedLog = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => capturedLog = log)
            .ReturnsAsync((AuditLog log) => log);

        // Act
        await _service.LogAsync(
            userId: 1,
            username: "testuser",
            universityId: 1,
            action: "Update",
            entityType: "University",
            entityId: 1,
            entityName: "Test University",
            changes: changes);

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal(1, capturedLog.UserId);
        Assert.Equal("testuser", capturedLog.Username);
        Assert.Equal(1, capturedLog.UniversityId);
        Assert.Equal("Update", capturedLog.Action);
        Assert.Equal("University", capturedLog.EntityType);
        Assert.Equal(1, capturedLog.EntityId);
        Assert.Equal("Test University", capturedLog.EntityName);
        Assert.NotNull(capturedLog.Changes);
        Assert.Contains("Name", capturedLog.Changes);
        Assert.Contains("Old Name", capturedLog.Changes);
        Assert.Contains("New Name", capturedLog.Changes);
    }

    [Fact]
    public async Task LogAsync_NoChanges_CreatesAuditLogWithNullChanges()
    {
        // Arrange
        AuditLog? capturedLog = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => capturedLog = log)
            .ReturnsAsync((AuditLog log) => log);

        // Act
        await _service.LogAsync(
            userId: 1,
            username: "testuser",
            universityId: null,
            action: "Delete",
            entityType: "Course",
            entityId: 5,
            entityName: "Test Course",
            changes: null);

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal("Delete", capturedLog.Action);
        Assert.Null(capturedLog.Changes);
    }

    private User CreateSuperAdminUser()
    {
        return new User
        {
            UserId = 1,
            Username = "superadmin",
            Email = "admin@example.com",
            FirstName = "Super",
            LastName = "Admin",
            UserType = UserTypes.SuperAdmin,
            UniversityId = null,
            IsAdmin = false,
            IsActive = true
        };
    }

    private User CreateManagerUser(int universityId)
    {
        return new User
        {
            UserId = 2,
            Username = "manager",
            Email = "manager@example.com",
            FirstName = "Test",
            LastName = "Manager",
            UserType = UserTypes.Manager,
            UniversityId = universityId,
            IsAdmin = false,
            IsActive = true
        };
    }

    private User CreateProfessorUser()
    {
        return new User
        {
            UserId = 3,
            Username = "professor",
            Email = "professor@example.com",
            FirstName = "Test",
            LastName = "Professor",
            UserType = UserTypes.Professor,
            UniversityId = 1,
            IsAdmin = false,
            IsActive = true
        };
    }

    private AuditLog CreateAuditLog(
        int id,
        string action,
        string entityType,
        int? universityId = null,
        string? entityName = null,
        string? changes = null)
    {
        return new AuditLog
        {
            Id = id,
            UserId = 1,
            Username = "testuser",
            UniversityId = universityId,
            Action = action,
            EntityType = entityType,
            EntityId = id,
            EntityName = entityName,
            Changes = changes,
            CreatedAt = DateTime.UtcNow
        };
    }
}
