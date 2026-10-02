using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Per-institution external API keys: shown once, stored hashed, scoped to one
/// university, revocable — and only that university's admins can manage them.
/// </summary>
public class UniversityApiKeyServiceTests
{
    private readonly Mock<IUniversityApiKeyRepository> _keys = new();
    private readonly Mock<IUniversityRepository> _universities = new();
    private readonly UniversityApiKeyService _service;

    public UniversityApiKeyServiceTests()
    {
        _service = new UniversityApiKeyService(_keys.Object, _universities.Object,
            Mock.Of<ILogger<UniversityApiKeyService>>());

        _universities.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(
            new University { Id = 1, Name = "Uni Um", Code = "U1", HasAssignments = true });
        _keys.Setup(r => r.GetByUniversityAsync(It.IsAny<int>())).ReturnsAsync(new List<UniversityApiKey>());
        _keys.Setup(r => r.AddAsync(It.IsAny<UniversityApiKey>())).ReturnsAsync((UniversityApiKey k) => k);
    }

    private static User Manager(int? universityId) => new()
    {
        UserId = 7, Username = "m", Email = "m@x.com", FirstName = "M", LastName = "M",
        UserType = "manager", UniversityId = universityId,
    };

    private static User SuperAdmin() => new()
    {
        UserId = 1, Username = "s", Email = "s@x.com", FirstName = "S", LastName = "S",
        UserType = "super_admin",
    };

    [Fact]
    public async Task CreateAsync_ReturnsPlaintextOnce_AndStoresOnlyTheHash()
    {
        UniversityApiKey? stored = null;
        _keys.Setup(r => r.AddAsync(It.IsAny<UniversityApiKey>()))
            .Callback<UniversityApiKey>(k => stored = k)
            .ReturnsAsync((UniversityApiKey k) => k);

        var created = await _service.CreateAsync(1, "  Moodle produção  ", Manager(1));

        Assert.StartsWith("tgk_", created.PlainTextKey);
        Assert.Equal(68, created.PlainTextKey.Length);
        Assert.NotNull(stored);
        Assert.Equal(UniversityApiKeyService.Hash(created.PlainTextKey), stored!.KeyHash);
        Assert.DoesNotContain(created.PlainTextKey, stored.KeyHash);
        Assert.Equal(created.PlainTextKey[..10], stored.KeyPrefix);
        Assert.Equal("Moodle produção", stored.Name);
        Assert.Equal(1, stored.UniversityId);
        Assert.Equal(7, stored.CreatedByUserId);
    }

    [Fact]
    public async Task CreateAsync_EveryKeyIsDifferent()
    {
        var a = await _service.CreateAsync(1, "a", Manager(1));
        var b = await _service.CreateAsync(1, "b", Manager(1));
        Assert.NotEqual(a.PlainTextKey, b.PlainTextKey);
    }

    [Fact]
    public async Task CreateAsync_ManagerOfAnotherUniversity_IsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateAsync(1, "x", Manager(2)));
        _keys.Verify(r => r.AddAsync(It.IsAny<UniversityApiKey>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UserWithoutUniversity_IsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateAsync(1, "x", Manager(null)));
    }

    [Fact]
    public async Task CreateAsync_SuperAdmin_CanCreateForAnyUniversity()
    {
        var created = await _service.CreateAsync(1, "x", SuperAdmin());
        Assert.Equal(1, created.Key.UniversityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankName_IsRejected(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(1, name, Manager(1)));
    }

    [Fact]
    public async Task CreateAsync_UnknownUniversity_Throws()
    {
        _universities.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((University?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(99, "x", SuperAdmin()));
    }

    [Fact]
    public async Task CreateAsync_AtActiveKeyLimit_IsRejected()
    {
        var full = Enumerable.Range(1, UniversityApiKeyService.MaxActiveKeysPerUniversity)
            .Select(i => new UniversityApiKey { Id = i, UniversityId = 1, Name = "k", KeyHash = $"h{i}", KeyPrefix = "tgk_" })
            .ToList();
        _keys.Setup(r => r.GetByUniversityAsync(1)).ReturnsAsync(full);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(1, "x", Manager(1)));
    }

    [Fact]
    public async Task CreateAsync_RevokedKeysDontCountTowardsTheLimit()
    {
        var revoked = Enumerable.Range(1, UniversityApiKeyService.MaxActiveKeysPerUniversity)
            .Select(i => new UniversityApiKey { Id = i, UniversityId = 1, Name = "k", KeyHash = $"h{i}", KeyPrefix = "tgk_", RevokedAt = DateTime.UtcNow })
            .ToList();
        _keys.Setup(r => r.GetByUniversityAsync(1)).ReturnsAsync(revoked);

        var created = await _service.CreateAsync(1, "x", Manager(1));
        Assert.NotNull(created);
    }

    [Fact]
    public async Task ValidateAsync_ActiveKey_ReturnsItsUniversity()
    {
        var plain = UniversityApiKeyService.GenerateKey();
        _keys.Setup(r => r.GetActiveByHashAsync(UniversityApiKeyService.Hash(plain)))
            .ReturnsAsync(new UniversityApiKey { Id = 3, UniversityId = 1, Name = "k", KeyHash = "h", KeyPrefix = "tgk_" });

        Assert.Equal(1, await _service.ValidateAsync(plain));
    }

    [Fact]
    public async Task ValidateAsync_UnknownOrRevokedKey_ReturnsNull()
    {
        _keys.Setup(r => r.GetActiveByHashAsync(It.IsAny<string>())).ReturnsAsync((UniversityApiKey?)null);
        Assert.Null(await _service.ValidateAsync(UniversityApiKeyService.GenerateKey()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("the-legacy-shared-key")]
    public async Task ValidateAsync_NonInstitutionKey_SkipsTheDatabase(string? key)
    {
        Assert.Null(await _service.ValidateAsync(key));
        _keys.Verify(r => r.GetActiveByHashAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_RecordsLastUse_ButNotOnEveryCall()
    {
        var plain = UniversityApiKeyService.GenerateKey();
        var key = new UniversityApiKey { Id = 3, UniversityId = 1, Name = "k", KeyHash = "h", KeyPrefix = "tgk_" };
        _keys.Setup(r => r.GetActiveByHashAsync(It.IsAny<string>())).ReturnsAsync(key);

        await _service.ValidateAsync(plain);   // first use → recorded
        await _service.ValidateAsync(plain);   // seconds later → not written again

        Assert.NotNull(key.LastUsedAt);
        _keys.Verify(r => r.UpdateAsync(key), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_SetsRevokedAt()
    {
        var key = new UniversityApiKey { Id = 3, UniversityId = 1, Name = "k", KeyHash = "h", KeyPrefix = "tgk_" };
        _keys.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(key);

        await _service.RevokeAsync(1, 3, Manager(1));

        Assert.NotNull(key.RevokedAt);
        _keys.Verify(r => r.UpdateAsync(key), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_KeyOfAnotherUniversity_IsNotFound()
    {
        _keys.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(
            new UniversityApiKey { Id = 3, UniversityId = 2, Name = "k", KeyHash = "h", KeyPrefix = "tgk_" });

        // A super admin passing the wrong university can't revoke another's key either.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RevokeAsync(1, 3, SuperAdmin()));
        _keys.Verify(r => r.UpdateAsync(It.IsAny<UniversityApiKey>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_ManagerOfAnotherUniversity_IsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RevokeAsync(1, 3, Manager(2)));
    }

    [Fact]
    public async Task GetSetupInfoAsync_ReportsWhetherGradingIsEnabled()
    {
        var info = await _service.GetSetupInfoAsync(1, Manager(1));
        Assert.Equal(1, info.UniversityId);
        Assert.Equal("Uni Um", info.UniversityName);
        Assert.True(info.Enabled);
    }
}
