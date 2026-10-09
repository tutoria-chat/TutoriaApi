using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class UserTokenServiceTests
{
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IPermissionService> _permissions = new();
    private readonly UserTokenService _service;
    private readonly User _student = new()
    {
        UserId = 9, Username = "ana@x.com", Email = "ana@x.com", FirstName = "Ana", LastName = "Souza",
        UserType = "student", UniversityId = 3,
    };

    public UserTokenServiceTests()
    {
        _permissions.Setup(p => p.GetUserEffectivePermissionsAsync(9, "student")).ReturnsAsync(new List<Permission>());
        _service = new UserTokenService(_jwt.Object, _permissions.Object);
    }

    [Fact]
    public async Task BuildClaimsAsync_StudentIncludesUniversityAndIdentity()
    {
        var claims = await _service.BuildClaimsAsync(_student);
        Assert.Equal("9", claims["user_id"]);
        Assert.Equal("student", claims["user_type"]);
        Assert.Equal("3", claims["UniversityId"]);
        Assert.Equal("[]", claims["permissions"]);
    }

    [Fact]
    public async Task IssueAsync_StudentGetsReadScopeAnd8hToken()
    {
        _jwt.Setup(j => j.GenerateToken("9", "student", new[] { "api.read" }, 480, It.IsAny<IDictionary<string, string>>())).Returns("a");
        _jwt.Setup(j => j.GenerateRefreshToken("9", "student", new[] { "api.read" }, It.IsAny<IDictionary<string, string>>())).Returns("r");
        var (access, refresh) = await _service.IssueAsync(_student);
        Assert.Equal(("a", "r"), (access, refresh));
    }

    [Fact]
    public void ScopesFor_UnknownType_IsEmpty()
        => Assert.Empty(_service.ScopesFor(new User { Username = "x", Email = "x", FirstName = "x", LastName = "x", UserType = "alien" }));
}
