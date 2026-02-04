using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using TutoriaApi.Core.Constants;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class CurrentUserServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly CurrentUserService _service;

    public CurrentUserServiceTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _service = new CurrentUserService(_httpContextAccessorMock.Object);
    }

    [Fact]
    public void GetCurrentUser_ValidClaims_ReturnsUser()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor),
            new Claim("UniversityId", "1"),
            new Claim("isAdmin", "true")
        });

        // Act
        var user = _service.GetCurrentUser();

        // Assert
        Assert.NotNull(user);
        Assert.Equal(1, user.UserId);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal(UserTypes.Professor, user.UserType);
        Assert.Equal(1, user.UniversityId);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public void GetCurrentUser_CachesResult_ReturnsSameInstanceOnSecondCall()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor)
        });

        // Act
        var user1 = _service.GetCurrentUser();
        var user2 = _service.GetCurrentUser();

        // Assert
        Assert.Same(user1, user2);
    }

    [Fact]
    public void GetUserId_ValidClaims_ReturnsUserId()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor)
        });

        // Act
        var userId = _service.GetUserId();

        // Assert
        Assert.Equal(42, userId);
    }

    [Fact]
    public void GetUsername_ValidClaims_ReturnsUsername()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "john.doe"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor)
        });

        // Act
        var username = _service.GetUsername();

        // Assert
        Assert.Equal("john.doe", username);
    }

    [Fact]
    public void GetUserType_ValidClaims_ReturnsUserType()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Manager)
        });

        // Act
        var userType = _service.GetUserType();

        // Assert
        Assert.Equal(UserTypes.Manager, userType);
    }

    [Fact]
    public void GetUniversityId_WithUniversityIdClaim_ReturnsUniversityId()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor),
            new Claim("UniversityId", "5")
        });

        // Act
        var universityId = _service.GetUniversityId();

        // Assert
        Assert.Equal(5, universityId);
    }

    [Fact]
    public void GetUniversityId_WithoutUniversityIdClaim_ReturnsNull()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.SuperAdmin)
        });

        // Act
        var universityId = _service.GetUniversityId();

        // Assert
        Assert.Null(universityId);
    }

    [Fact]
    public void IsSuperAdmin_SuperAdminRole_ReturnsTrue()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.SuperAdmin)
        });

        // Act
        var result = _service.IsSuperAdmin();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSuperAdmin_ProfessorRole_ReturnsFalse()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "professor"),
            new Claim(ClaimTypes.Email, "prof@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor)
        });

        // Act
        var result = _service.IsSuperAdmin();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasAdminPrivileges_SuperAdmin_ReturnsTrue()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.SuperAdmin)
        });

        // Act
        var result = _service.HasAdminPrivileges();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAdminPrivileges_Manager_ReturnsTrue()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "manager"),
            new Claim(ClaimTypes.Email, "manager@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Manager)
        });

        // Act
        var result = _service.HasAdminPrivileges();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAdminPrivileges_AdminProfessor_ReturnsTrue()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "professor"),
            new Claim(ClaimTypes.Email, "prof@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor),
            new Claim("isAdmin", "true")
        });

        // Act
        var result = _service.HasAdminPrivileges();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAdminPrivileges_RegularProfessor_ReturnsFalse()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "professor"),
            new Claim(ClaimTypes.Email, "prof@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor),
            new Claim("isAdmin", "false")
        });

        // Act
        var result = _service.HasAdminPrivileges();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAuthenticated_WithValidContext_ReturnsTrue()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, UserTypes.Professor)
        });

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAuthenticated_WithoutContext_ReturnsFalse()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCurrentUser_NoHttpContext_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => _service.GetCurrentUser());
    }

    [Fact]
    public void GetCurrentUser_NoUserIdClaim_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        SetupHttpContext(new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        });

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => _service.GetCurrentUser());
    }

    private void SetupHttpContext(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }
}
