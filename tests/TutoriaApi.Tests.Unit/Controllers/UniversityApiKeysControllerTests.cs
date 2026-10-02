using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

public class UniversityApiKeysControllerTests
{
    private readonly Mock<IUniversityApiKeyService> _service = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly UniversityApiKeysController _controller;

    public UniversityApiKeysControllerTests()
    {
        _currentUser.Setup(c => c.GetCurrentUser()).Returns(new User
        {
            UserId = 7, Username = "m", Email = "m@x.com", FirstName = "M", LastName = "M",
            UserType = "manager", UniversityId = 1,
        });
        _controller = new UniversityApiKeysController(_service.Object, _currentUser.Object,
            Mock.Of<ILogger<UniversityApiKeysController>>());
    }

    private static UniversityApiKey Key(int id = 3, DateTime? revokedAt = null) => new()
    {
        Id = id, UniversityId = 1, Name = "Moodle", KeyHash = "secret-hash", KeyPrefix = "tgk_3f9a1c",
        CreatedAt = DateTime.UtcNow, RevokedAt = revokedAt,
    };

    [Fact]
    public async Task Create_Returns201_WithTheFullKeyOnce()
    {
        _service.Setup(s => s.CreateAsync(1, "Moodle", It.IsAny<User>()))
            .ReturnsAsync(new CreatedUniversityApiKey { Key = Key(), PlainTextKey = "tgk_full_key" });

        var result = await _controller.Create(1, new CreateUniversityApiKeyRequest { Name = "Moodle" });

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(201, obj.StatusCode);
        var dto = Assert.IsType<CreatedUniversityApiKeyDto>(obj.Value);
        Assert.Equal("tgk_full_key", dto.Key);
        Assert.Equal("tgk_3f9a1c", dto.KeyPrefix);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task GetAll_NeverReturnsTheKeyOrItsHash()
    {
        _service.Setup(s => s.ListAsync(1, It.IsAny<User>()))
            .ReturnsAsync(new List<UniversityApiKey> { Key(), Key(4, DateTime.UtcNow) });

        var result = await _controller.GetAll(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<UniversityApiKeyDto>>(ok.Value).ToList();
        Assert.Equal(2, dtos.Count);
        Assert.All(dtos, d => Assert.IsNotType<CreatedUniversityApiKeyDto>(d));
        Assert.True(dtos[0].IsActive);
        Assert.False(dtos[1].IsActive);
    }

    [Fact]
    public async Task Create_OtherInstitution_Is403()
    {
        _service.Setup(s => s.CreateAsync(2, It.IsAny<string>(), It.IsAny<User>()))
            .ThrowsAsync(new UnauthorizedAccessException("nope"));

        var result = await _controller.Create(2, new CreateUniversityApiKeyRequest { Name = "x" });

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Create_AtLimit_Is409()
    {
        _service.Setup(s => s.CreateAsync(1, It.IsAny<string>(), It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("limit"));

        var result = await _controller.Create(1, new CreateUniversityApiKeyRequest { Name = "x" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Revoke_Returns204()
    {
        var result = await _controller.Revoke(1, 3);
        Assert.IsType<NoContentResult>(result);
        _service.Verify(s => s.RevokeAsync(1, 3, It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_UnknownKey_Is404()
    {
        _service.Setup(s => s.RevokeAsync(1, 9, It.IsAny<User>())).ThrowsAsync(new KeyNotFoundException());
        Assert.IsType<NotFoundObjectResult>(await _controller.Revoke(1, 9));
    }

    [Fact]
    public async Task GetSetupInfo_ReturnsInstitutionAndEnabledFlag()
    {
        _service.Setup(s => s.GetSetupInfoAsync(1, It.IsAny<User>())).ReturnsAsync(
            new UniversityApiKeySetupInfo { UniversityId = 1, UniversityName = "Uni Um", Enabled = true });

        var result = await _controller.GetSetupInfo(1);

        var dto = Assert.IsType<UniversityApiKeySetupInfoDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, dto.UniversityId);
        Assert.True(dto.Enabled);
    }

    [Fact]
    public async Task UnexpectedError_Is500()
    {
        _service.Setup(s => s.ListAsync(1, It.IsAny<User>())).ThrowsAsync(new Exception("db down"));
        var result = await _controller.GetAll(1);
        Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }
}
