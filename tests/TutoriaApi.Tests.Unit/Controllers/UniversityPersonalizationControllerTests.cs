using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Tests.Unit.Controllers;

/// <summary>
/// Tests for the personalization endpoints on UniversitiesController.
/// GET  /api/universities/{id}/personalization
/// PUT  /api/universities/{id}/personalization
/// </summary>
public class UniversityPersonalizationControllerTests
{
    private readonly Mock<IUniversityService> _universityServiceMock;
    private readonly Mock<IStudentService> _studentServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IUniversityPersonalizationService> _personalizationServiceMock;
    private readonly Mock<ILogger<UniversitiesController>> _loggerMock;
    private readonly UniversitiesController _controller;

    public UniversityPersonalizationControllerTests()
    {
        _universityServiceMock = new Mock<IUniversityService>();
        _studentServiceMock = new Mock<IStudentService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _personalizationServiceMock = new Mock<IUniversityPersonalizationService>();
        _loggerMock = new Mock<ILogger<UniversitiesController>>();

        _controller = new UniversitiesController(
            _universityServiceMock.Object,
            _studentServiceMock.Object,
            _currentUserServiceMock.Object,
            _personalizationServiceMock.Object,
            _loggerMock.Object);

        SetupControllerContext();
    }

    private void SetupControllerContext(string userType = "super_admin", int? universityId = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testadmin"),
            new Claim(ClaimTypes.Email, "admin@test.com"),
            new Claim(ClaimTypes.Role, userType)
        };

        if (universityId.HasValue)
            claims.Add(new Claim("UniversityId", universityId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    // ── GET {id}/personalization ───────────────────────────────────────────────

    [Fact]
    public async Task GetPersonalization_ExistingRecord_ReturnsOkWithColors()
    {
        var personalization = new UniversityPersonalization
        {
            Id = 1,
            UniversityId = 5,
            PrimaryColor = "#7C3AED",
            SecondaryColor = "#F3F4F6",
            DefaultTheme = "dark"
        };

        _personalizationServiceMock.Setup(s => s.GetByUniversityIdAsync(5))
            .ReturnsAsync(personalization);

        var result = await _controller.GetPersonalization(5);

        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value!;

        // Verify using anonymous object property access
        var valueType = okResult.Value!.GetType();
        Assert.Equal("#7C3AED", valueType.GetProperty("primaryColor")!.GetValue(okResult.Value)!.ToString());
        Assert.Equal("#F3F4F6", valueType.GetProperty("secondaryColor")!.GetValue(okResult.Value)!.ToString());
        Assert.Equal("dark", valueType.GetProperty("defaultTheme")!.GetValue(okResult.Value)!.ToString());
    }

    [Fact]
    public async Task GetPersonalization_NoRecord_ReturnsOkWithDefaults()
    {
        _personalizationServiceMock.Setup(s => s.GetByUniversityIdAsync(5))
            .ReturnsAsync((UniversityPersonalization?)null);

        var result = await _controller.GetPersonalization(5);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        Assert.Null(valueType.GetProperty("primaryColor")!.GetValue(okResult.Value));
        Assert.Null(valueType.GetProperty("secondaryColor")!.GetValue(okResult.Value));
        Assert.Equal("auto", valueType.GetProperty("defaultTheme")!.GetValue(okResult.Value)!.ToString());
    }

    [Fact]
    public async Task GetPersonalization_ServiceThrows_Returns500()
    {
        _personalizationServiceMock.Setup(s => s.GetByUniversityIdAsync(5))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetPersonalization(5);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    // ── PUT {id}/personalization ───────────────────────────────────────────────

    [Fact]
    public async Task UpsertPersonalization_ValidRequest_ReturnsOkWithResult()
    {
        var currentUser = new User
        {
            UserId = 1,
            Username = "admin",
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            UserType = "super_admin",
            UniversityId = null
        };
        _currentUserServiceMock.Setup(s => s.GetCurrentUser()).Returns(currentUser);

        var saved = new UniversityPersonalization
        {
            Id = 1,
            UniversityId = 5,
            PrimaryColor = "#7C3AED",
            SecondaryColor = "#F3F4F6",
            DefaultTheme = "light",
            UpdatedAt = DateTime.UtcNow
        };
        _personalizationServiceMock.Setup(s => s.UpsertAsync(
                5, "#7C3AED", "#F3F4F6", "light", 1, "super_admin", null))
            .ReturnsAsync(saved);

        var request = new UniversityAppearanceUpdateRequest
        {
            WidgetPrimaryColor = "#7C3AED",
            WidgetSecondaryColor = "#F3F4F6",
            WidgetDefaultTheme = "light"
        };

        var result = await _controller.UpsertPersonalization(5, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        Assert.Equal("#7C3AED", valueType.GetProperty("primaryColor")!.GetValue(okResult.Value)!.ToString());
        Assert.Equal("light", valueType.GetProperty("defaultTheme")!.GetValue(okResult.Value)!.ToString());
    }

    [Fact]
    public async Task UpsertPersonalization_UniversityNotFound_ReturnsNotFound()
    {
        var currentUser = new User { UserId = 1, Username = "admin", Email = "admin@test.com", FirstName = "Admin", LastName = "User", UserType = "super_admin", UniversityId = null };
        _currentUserServiceMock.Setup(s => s.GetCurrentUser()).Returns(currentUser);

        _personalizationServiceMock.Setup(s => s.UpsertAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ThrowsAsync(new KeyNotFoundException("University not found"));

        var result = await _controller.UpsertPersonalization(99,
            new UniversityAppearanceUpdateRequest { WidgetDefaultTheme = "auto" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpsertPersonalization_InsufficientPermissions_ReturnsForbid()
    {
        var currentUser = new User { UserId = 2, Username = "prof", Email = "prof@test.com", FirstName = "Prof", LastName = "User", UserType = "professor", UniversityId = 5 };
        _currentUserServiceMock.Setup(s => s.GetCurrentUser()).Returns(currentUser);

        _personalizationServiceMock.Setup(s => s.UpsertAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ThrowsAsync(new UnauthorizedAccessException("Insufficient permissions"));

        var result = await _controller.UpsertPersonalization(5,
            new UniversityAppearanceUpdateRequest { WidgetDefaultTheme = "auto" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpsertPersonalization_ServiceThrows_Returns500()
    {
        var currentUser = new User { UserId = 1, Username = "admin", Email = "admin@test.com", FirstName = "Admin", LastName = "User", UserType = "super_admin", UniversityId = null };
        _currentUserServiceMock.Setup(s => s.GetCurrentUser()).Returns(currentUser);

        _personalizationServiceMock.Setup(s => s.UpsertAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var result = await _controller.UpsertPersonalization(5,
            new UniversityAppearanceUpdateRequest { WidgetDefaultTheme = "auto" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}
