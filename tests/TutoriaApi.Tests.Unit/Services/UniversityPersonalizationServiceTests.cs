using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;

namespace TutoriaApi.Tests.Unit.Services;

public class UniversityPersonalizationServiceTests
{
    private readonly Mock<IUniversityPersonalizationRepository> _repoMock;
    private readonly Mock<IUniversityRepository> _universityRepoMock;
    private readonly Mock<IAuditLogService> _auditLogMock;
    private readonly UniversityPersonalizationService _service;

    // Reusable audit-log setup — matches any call to LogAsync
    private static readonly University TestUniversity = new()
    {
        Id = 5,
        Name = "Test University",
        Code = "TUNI"
    };

    public UniversityPersonalizationServiceTests()
    {
        _repoMock = new Mock<IUniversityPersonalizationRepository>();
        _universityRepoMock = new Mock<IUniversityRepository>();
        _auditLogMock = new Mock<IAuditLogService>();

        _service = new UniversityPersonalizationService(
            _repoMock.Object,
            _universityRepoMock.Object,
            _auditLogMock.Object);
    }

    // Convenience: set up the audit log mock to accept any call
    private void SetupAuditLog()
    {
        _auditLogMock.Setup(a => a.LogAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, (object? OldValue, object? NewValue)>?>()))
            .Returns(Task.CompletedTask);
    }

    // ── GetByUniversityIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByUniversityIdAsync_ExistingRecord_ReturnsPersonalization()
    {
        var personalization = new UniversityPersonalization
        {
            Id = 1,
            UniversityId = 10,
            PrimaryColor = "#7C3AED",
            SecondaryColor = "#F3F4F6",
            DefaultTheme = "auto"
        };

        _repoMock.Setup(r => r.GetByUniversityIdAsync(10)).ReturnsAsync(personalization);

        var result = await _service.GetByUniversityIdAsync(10);

        Assert.NotNull(result);
        Assert.Equal("#7C3AED", result.PrimaryColor);
        Assert.Equal("auto", result.DefaultTheme);
    }

    [Fact]
    public async Task GetByUniversityIdAsync_NoRecord_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByUniversityIdAsync(99)).ReturnsAsync((UniversityPersonalization?)null);

        var result = await _service.GetByUniversityIdAsync(99);

        Assert.Null(result);
    }

    // ── UpsertAsync — authorization ────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_ProfessorRole_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpsertAsync(5, "#7C3AED", null, "auto", null, 1, UserTypes.Professor, 5));
    }

    [Fact]
    public async Task UpsertAsync_ManagerForDifferentUniversity_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpsertAsync(5, "#7C3AED", null, "auto", null, 1, UserTypes.Manager, 99));
    }

    [Fact]
    public async Task UpsertAsync_UniversityNotFound_ThrowsKeyNotFoundException()
    {
        _universityRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((University?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpsertAsync(5, "#7C3AED", null, "auto", null, 1, UserTypes.SuperAdmin, null));
    }

    // ── UpsertAsync — create path ──────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_SuperAdmin_NoExistingRecord_CreatesNewPersonalization()
    {
        SetupAuditLog();
        _universityRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(TestUniversity);
        _repoMock.Setup(r => r.GetByUniversityIdAsync(5)).ReturnsAsync((UniversityPersonalization?)null);

        var created = new UniversityPersonalization
        {
            Id = 1, UniversityId = 5, PrimaryColor = "#7C3AED",
            SecondaryColor = "#F3F4F6", DefaultTheme = "dark"
        };
        _repoMock.Setup(r => r.AddAsync(It.IsAny<UniversityPersonalization>())).ReturnsAsync(created);

        var result = await _service.UpsertAsync(5, "#7C3AED", "#F3F4F6", "dark", 80, 1, UserTypes.SuperAdmin, null);

        Assert.NotNull(result);
        Assert.Equal("#7C3AED", result.PrimaryColor);
        Assert.Equal("dark", result.DefaultTheme);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UniversityPersonalization>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>()), Times.Never);
    }

    // ── UpsertAsync — update path ──────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_ManagerForOwnUniversity_ExistingRecord_UpdatesPersonalization()
    {
        SetupAuditLog();
        _universityRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(TestUniversity);
        var existing = new UniversityPersonalization
        {
            Id = 1, UniversityId = 5, PrimaryColor = "#000000", DefaultTheme = "auto"
        };
        _repoMock.Setup(r => r.GetByUniversityIdAsync(5)).ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>())).Returns(Task.CompletedTask);

        var result = await _service.UpsertAsync(5, "#7C3AED", null, "light", 70, 2, UserTypes.Manager, 5);

        Assert.Equal("#7C3AED", result.PrimaryColor);
        Assert.Null(result.SecondaryColor);
        Assert.Equal("light", result.DefaultTheme);
        Assert.Equal(70, result.BubbleOpacity);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UniversityPersonalization>()), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_PlatformCoordinatorForOwnUniversity_UpdatesPersonalization()
    {
        SetupAuditLog();
        var university = new University { Id = 7, Name = "Coordinator University", Code = "CU" };
        var existing = new UniversityPersonalization { Id = 2, UniversityId = 7, DefaultTheme = "auto" };

        _universityRepoMock.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(university);
        _repoMock.Setup(r => r.GetByUniversityIdAsync(7)).ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>())).Returns(Task.CompletedTask);

        var result = await _service.UpsertAsync(7, "#2563EB", "#F3F4F6", "dark", null, 3, UserTypes.PlatformCoordinator, 7);

        Assert.Equal("#2563EB", result.PrimaryColor);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>()), Times.Once);
    }

    // ── Audit log verification ─────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_AuditLogCalledWithCreateAction_OnNewRecord()
    {
        SetupAuditLog();
        _universityRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(TestUniversity);
        _repoMock.Setup(r => r.GetByUniversityIdAsync(5)).ReturnsAsync((UniversityPersonalization?)null);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<UniversityPersonalization>()))
            .ReturnsAsync(new UniversityPersonalization { Id = 1, UniversityId = 5, DefaultTheme = "auto" });

        await _service.UpsertAsync(5, null, null, "auto", null, 1, UserTypes.SuperAdmin, null);

        _auditLogMock.Verify(a => a.LogAsync(
            1, It.IsAny<string>(), 5, "Create", "UniversityPersonalization",
            It.IsAny<int>(), "Test University",
            It.IsAny<Dictionary<string, (object? OldValue, object? NewValue)>?>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_AuditLogCalledWithUpdateAction_OnExistingRecord()
    {
        SetupAuditLog();
        _universityRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(TestUniversity);
        var existing = new UniversityPersonalization { Id = 1, UniversityId = 5, DefaultTheme = "auto" };
        _repoMock.Setup(r => r.GetByUniversityIdAsync(5)).ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<UniversityPersonalization>())).Returns(Task.CompletedTask);

        await _service.UpsertAsync(5, "#7C3AED", null, "dark", null, 1, UserTypes.SuperAdmin, null);

        _auditLogMock.Verify(a => a.LogAsync(
            1, It.IsAny<string>(), 5, "Update", "UniversityPersonalization",
            1, "Test University",
            It.IsAny<Dictionary<string, (object? OldValue, object? NewValue)>?>()), Times.Once);
    }
}
