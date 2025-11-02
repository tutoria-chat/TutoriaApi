using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

public class ModuleAccessTokensControllerTests
{
    private readonly Mock<IModuleAccessTokenService> _serviceMock;
    private readonly Mock<ILogger<ModuleAccessTokensController>> _loggerMock;
    private readonly ModuleAccessTokensController _controller;

    public ModuleAccessTokensControllerTests()
    {
        _serviceMock = new Mock<IModuleAccessTokenService>();
        _loggerMock = new Mock<ILogger<ModuleAccessTokensController>>();
        _controller = new ModuleAccessTokensController(_serviceMock.Object, _loggerMock.Object);

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "professor"),
            new Claim("user_type", "professor"),
            new Claim("university_id", "1"),
            new Claim("isAdmin", "false")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetModuleAccessTokens_ValidRequest_ReturnsOkWithPaginatedResponse()
    {
        // Arrange
        var viewModels = new List<ModuleAccessTokenListViewModel>
        {
            new ModuleAccessTokenListViewModel
            {
                Token = new ModuleAccessToken
                {
                    Id = 1,
                    Token = "token1",
                    Name = "Token 1",
                    ModuleId = 1,
                    IsActive = true,
                    AllowChat = true,
                    AllowFileAccess = true,
                    UsageCount = 5,
                    CreatedAt = DateTime.UtcNow
                },
                ModuleName = "Module 1"
            }
        };

        _serviceMock.Setup(s => s.GetPagedAsync(null, null, null, 1, 10, It.IsAny<User>()))
            .ReturnsAsync((viewModels, 1));

        // Act
        var result = await _controller.GetModuleAccessTokens(1, 10, null, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaginatedResponse<ModuleAccessTokenListDto>>(okResult.Value);
        Assert.Equal(1, response.Total);
        Assert.Single(response.Items);
        Assert.Equal("Token 1", response.Items.First().Name);
    }

    [Fact]
    public async Task GetModuleAccessTokens_WithFilters_CallsServiceWithCorrectParameters()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetPagedAsync(1, 2, true, 1, 10, It.IsAny<User>()))
            .ReturnsAsync((new List<ModuleAccessTokenListViewModel>(), 0));

        // Act
        await _controller.GetModuleAccessTokens(1, 10, 1, 2, true);

        // Assert
        _serviceMock.Verify(s => s.GetPagedAsync(1, 2, true, 1, 10, It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task GetModuleAccessTokens_InvalidPageNumber_NormalizesToOne()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetPagedAsync(null, null, null, 1, 10, It.IsAny<User>()))
            .ReturnsAsync((new List<ModuleAccessTokenListViewModel>(), 0));

        // Act
        await _controller.GetModuleAccessTokens(-5, 10, null, null, null);

        // Assert
        _serviceMock.Verify(s => s.GetPagedAsync(null, null, null, 1, 10, It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task GetModuleAccessTokens_SizeExceedsLimit_NormalizesToMax()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetPagedAsync(null, null, null, 1, 100, It.IsAny<User>()))
            .ReturnsAsync((new List<ModuleAccessTokenListViewModel>(), 0));

        // Act
        await _controller.GetModuleAccessTokens(1, 200, null, null, null);

        // Assert
        _serviceMock.Verify(s => s.GetPagedAsync(null, null, null, 1, 100, It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task GetModuleAccessTokens_ServiceThrowsException_Returns500()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetPagedAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<User>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetModuleAccessTokens(1, 10, null, null, null);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetModuleAccessToken_ExistingToken_ReturnsOkWithDetails()
    {
        // Arrange
        var viewModel = new ModuleAccessTokenDetailViewModel
        {
            Token = new ModuleAccessToken
            {
                Id = 1,
                Token = "test-token",
                Name = "Test Token",
                ModuleId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            ModuleName = "Test Module",
            CourseName = "Test Course",
            UniversityName = "Test University",
            CreatedByName = "John Doe"
        };

        _serviceMock.Setup(s => s.GetWithDetailsAsync(1))
            .ReturnsAsync(viewModel);

        // Act
        var result = await _controller.GetModuleAccessToken(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ModuleAccessTokenDetailDto>(okResult.Value);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Token", dto.Name);
        Assert.Equal("Test Module", dto.ModuleName);
    }

    [Fact]
    public async Task GetModuleAccessToken_NonExistentToken_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetWithDetailsAsync(999))
            .ReturnsAsync((ModuleAccessTokenDetailViewModel?)null);

        // Act
        var result = await _controller.GetModuleAccessToken(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetModuleAccessToken_ServiceThrowsException_Returns500()
    {
        // Arrange
        _serviceMock.Setup(s => s.GetWithDetailsAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetModuleAccessToken(1);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateModuleAccessToken_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new ModuleAccessTokenCreateRequest
        {
            ModuleId = 1,
            Name = "New Token",
            Description = "Test Description",
            AllowChat = true,
            AllowFileAccess = true,
            ExpiresInDays = 30
        };

        var viewModel = new ModuleAccessTokenDetailViewModel
        {
            Token = new ModuleAccessToken
            {
                Id = 1,
                Token = "generated-token-123",
                Name = "New Token",
                Description = "Test Description",
                ModuleId = 1,
                IsActive = true,
                AllowChat = true,
                AllowFileAccess = true,
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            ModuleName = "Test Module",
            CourseName = "Test Course",
            UniversityName = "Test University"
        };

        _serviceMock.Setup(s => s.CreateAsync(
                request.ModuleId,
                request.Name,
                request.Description,
                request.AllowChat,
                request.AllowFileAccess,
                request.ExpiresInDays,
                It.IsAny<User>()))
            .ReturnsAsync(viewModel);

        // Act
        var result = await _controller.CreateModuleAccessToken(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ModuleAccessTokensController.GetModuleAccessToken), createdResult.ActionName);
        var dto = Assert.IsType<ModuleAccessTokenDetailDto>(createdResult.Value);
        Assert.Equal(1, dto.Id);
        Assert.Equal("New Token", dto.Name);
    }

    [Fact]
    public async Task CreateModuleAccessToken_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new ModuleAccessTokenCreateRequest();
        _controller.ModelState.AddModelError("Name", "Name is required");

        // Act
        var result = await _controller.CreateModuleAccessToken(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateModuleAccessToken_NonExistentModule_ReturnsNotFound()
    {
        // Arrange
        var request = new ModuleAccessTokenCreateRequest
        {
            ModuleId = 999,
            Name = "New Token",
            AllowChat = true,
            AllowFileAccess = false
        };

        _serviceMock.Setup(s => s.CreateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("Module not found"));

        // Act
        var result = await _controller.CreateModuleAccessToken(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateModuleAccessToken_ServiceThrowsException_Returns500()
    {
        // Arrange
        var request = new ModuleAccessTokenCreateRequest
        {
            ModuleId = 1,
            Name = "New Token",
            AllowChat = true,
            AllowFileAccess = false
        };

        _serviceMock.Setup(s => s.CreateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.CreateModuleAccessToken(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateModuleAccessToken_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ModuleAccessTokenUpdateRequest
        {
            Name = "Updated Token",
            Description = "Updated Description",
            IsActive = false,
            AllowChat = true,
            AllowFileAccess = false
        };

        var viewModel = new ModuleAccessTokenDetailViewModel
        {
            Token = new ModuleAccessToken
            {
                Id = 1,
                Token = "test-token",
                Name = "Updated Token",
                Description = "Updated Description",
                IsActive = false,
                AllowChat = true,
                AllowFileAccess = false,
                ModuleId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            ModuleName = "Test Module",
            CourseName = "Test Course",
            UniversityName = "Test University",
            CreatedByName = "John Doe"
        };

        _serviceMock.Setup(s => s.UpdateAsync(
                1,
                request.Name,
                request.Description,
                request.IsActive,
                request.AllowChat,
                request.AllowFileAccess))
            .ReturnsAsync(viewModel);

        // Act
        var result = await _controller.UpdateModuleAccessToken(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ModuleAccessTokenDetailDto>(okResult.Value);
        Assert.Equal("Updated Token", dto.Name);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task UpdateModuleAccessToken_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new ModuleAccessTokenUpdateRequest();
        _controller.ModelState.AddModelError("Name", "Name is required");

        // Act
        var result = await _controller.UpdateModuleAccessToken(1, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateModuleAccessToken_NonExistentToken_ReturnsNotFound()
    {
        // Arrange
        var request = new ModuleAccessTokenUpdateRequest
        {
            Name = "Updated Token"
        };

        _serviceMock.Setup(s => s.UpdateAsync(
                999,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>()))
            .ThrowsAsync(new KeyNotFoundException("Module access token not found"));

        // Act
        var result = await _controller.UpdateModuleAccessToken(999, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateModuleAccessToken_ServiceThrowsException_Returns500()
    {
        // Arrange
        var request = new ModuleAccessTokenUpdateRequest
        {
            Name = "Updated Token"
        };

        _serviceMock.Setup(s => s.UpdateAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.UpdateModuleAccessToken(1, request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteModuleAccessToken_ExistingToken_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(s => s.DeleteAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteModuleAccessToken(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteModuleAccessToken_NonExistentToken_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(s => s.DeleteAsync(999))
            .ThrowsAsync(new KeyNotFoundException("Module access token not found"));

        // Act
        var result = await _controller.DeleteModuleAccessToken(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteModuleAccessToken_ServiceThrowsException_Returns500()
    {
        // Arrange
        _serviceMock.Setup(s => s.DeleteAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.DeleteModuleAccessToken(1);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}
