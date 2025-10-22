using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Controllers;

public class VideosControllerTests
{
    private readonly Mock<IVideoTranscriptionService> _serviceMock;
    private readonly Mock<ILogger<VideosController>> _loggerMock;
    private readonly VideosController _controller;
    private readonly User _testUser;

    public VideosControllerTests()
    {
        _serviceMock = new Mock<IVideoTranscriptionService>();
        _loggerMock = new Mock<ILogger<VideosController>>();
        _controller = new VideosController(_serviceMock.Object, _loggerMock.Object);

        // Setup test user
        _testUser = new User
        {
            UserId = 1,
            Username = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            UserType = "professor",
            UniversityId = 1,
            IsActive = true
        };

        // Setup HttpContext with claims
        SetupControllerContext();
    }

    #region AddYoutubeVideo Tests

    [Fact]
    public async Task AddYoutubeVideo_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 1,
            Language = "pt-br",
            Name = "Test Video"
        };

        var file = new FileEntity
        {
            Id = 100,
            Name = request.Name,
            FileType = "youtube_transcript",
            ModuleId = request.ModuleId,
            TranscriptionStatus = "completed",
            TranscriptWordCount = 1000,
            VideoDurationSeconds = 600,
            SourceType = "youtube",
            TranscriptLanguage = "pt-br",
            TranscriptText = "This is a test transcript with more than 500 characters..." + new string('x', 500),
            IsActive = true
        };

        _serviceMock.Setup(s => s.TranscribeYoutubeVideoAsync(
                request.YoutubeUrl,
                request.ModuleId,
                request.Language,
                request.Name,
                It.IsAny<User>()))
            .ReturnsAsync(file);

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<TranscriptionResultDto>(createdResult.Value);
        Assert.Equal(100, dto.FileId);
        Assert.Equal("completed", dto.Status);
        Assert.Equal(1000, dto.WordCount);
        Assert.NotNull(dto.TranscriptPreview);
    }

    [Fact]
    public async Task AddYoutubeVideo_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 1,
            Language = "pt-br"
        };

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task AddYoutubeVideo_ModuleNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 999,
            Language = "pt-br"
        };

        _serviceMock.Setup(s => s.TranscribeYoutubeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("Module not found"));

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = notFoundResult.Value;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task AddYoutubeVideo_UnauthorizedAccess_ReturnsForbid()
    {
        // Arrange
        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 1,
            Language = "pt-br"
        };

        _serviceMock.Setup(s => s.TranscribeYoutubeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task AddYoutubeVideo_TranscriptionServiceError_Returns503()
    {
        // Arrange
        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 1,
            Language = "pt-br"
        };

        _serviceMock.Setup(s => s.TranscribeYoutubeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("Python API error"));

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task AddYoutubeVideo_UnexpectedException_Returns500()
    {
        // Arrange
        var request = new AddYoutubeVideoRequest
        {
            YoutubeUrl = "https://youtube.com/watch?v=test123",
            ModuleId = 1,
            Language = "pt-br"
        };

        _serviceMock.Setup(s => s.TranscribeYoutubeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<User>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.AddYoutubeVideo(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    #endregion

    #region GetTranscriptionStatus Tests

    [Fact]
    public async Task GetTranscriptionStatus_FileExists_ReturnsOk()
    {
        // Arrange
        var fileId = 1;
        var file = new FileEntity
        {
            Id = fileId,
            Name = "Test Video",
            FileType = "youtube_transcript",
            ModuleId = 1,
            TranscriptionStatus = "completed",
            TranscriptWordCount = 1000,
            VideoDurationSeconds = 600,
            TranscriptLanguage = "pt-br",
            SourceUrl = "https://youtube.com/watch?v=test123",
            SourceType = "youtube",
            TranscriptedAt = DateTime.UtcNow,
            TranscriptText = "Some transcript text",
            IsActive = true
        };

        _serviceMock.Setup(s => s.GetTranscriptionStatusAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(file);

        // Act
        var result = await _controller.GetTranscriptionStatus(fileId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TranscriptionStatusDto>(okResult.Value);
        Assert.Equal(fileId, dto.FileId);
        Assert.Equal("Test Video", dto.Name);
        Assert.Equal("completed", dto.Status);
        Assert.Equal(1000, dto.WordCount);
        Assert.True(dto.HasTranscript);
    }

    [Fact]
    public async Task GetTranscriptionStatus_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = 999;

        _serviceMock.Setup(s => s.GetTranscriptionStatusAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _controller.GetTranscriptionStatus(fileId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = notFoundResult.Value;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetTranscriptionStatus_UnauthorizedAccess_ReturnsForbid()
    {
        // Arrange
        var fileId = 1;

        _serviceMock.Setup(s => s.GetTranscriptionStatusAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.GetTranscriptionStatus(fileId);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    #endregion

    #region GetTranscriptText Tests

    [Fact]
    public async Task GetTranscriptText_FileWithTranscript_ReturnsOk()
    {
        // Arrange
        var fileId = 1;
        var file = new FileEntity
        {
            Id = fileId,
            Name = "Test Video",
            FileType = "youtube_transcript",
            ModuleId = 1,
            TranscriptText = "This is the full transcript text...",
            TranscriptWordCount = 100,
            TranscriptLanguage = "pt-br",
            IsActive = true
        };

        _serviceMock.Setup(s => s.GetTranscriptTextAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(file);

        // Act
        var result = await _controller.GetTranscriptText(fileId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TranscriptTextDto>(okResult.Value);
        Assert.Equal(fileId, dto.FileId);
        Assert.Equal("This is the full transcript text...", dto.Transcript);
        Assert.Equal(100, dto.WordCount);
        Assert.Equal("pt-br", dto.Language);
    }

    [Fact]
    public async Task GetTranscriptText_NoTranscript_ReturnsNotFound()
    {
        // Arrange
        var fileId = 1;

        _serviceMock.Setup(s => s.GetTranscriptTextAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _controller.GetTranscriptText(fileId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = notFoundResult.Value;
        Assert.NotNull(response);
    }

    #endregion

    #region RetryTranscription Tests

    [Fact]
    public async Task RetryTranscription_Success_ReturnsOk()
    {
        // Arrange
        var fileId = 1;
        var file = new FileEntity
        {
            Id = fileId,
            Name = "Test Video",
            FileType = "youtube_transcript",
            ModuleId = 1,
            TranscriptionStatus = "completed",
            TranscriptWordCount = 1000,
            VideoDurationSeconds = 600,
            SourceType = "whisper",
            TranscriptLanguage = "pt-br",
            TranscriptText = "Retried transcript text",
            IsActive = true
        };

        _serviceMock.Setup(s => s.RetryTranscriptionAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(file);

        // Act
        var result = await _controller.RetryTranscription(fileId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TranscriptionResultDto>(okResult.Value);
        Assert.Equal(fileId, dto.FileId);
        Assert.Equal("completed", dto.Status);
    }

    [Fact]
    public async Task RetryTranscription_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = 999;

        _serviceMock.Setup(s => s.RetryTranscriptionAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new KeyNotFoundException("File not found"));

        // Act
        var result = await _controller.RetryTranscription(fileId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = notFoundResult.Value;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task RetryTranscription_NotFailedStatus_ReturnsBadRequest()
    {
        // Arrange
        var fileId = 1;

        _serviceMock.Setup(s => s.RetryTranscriptionAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new InvalidOperationException("Only failed transcriptions can be retried"));

        // Act
        var result = await _controller.RetryTranscription(fileId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = badRequestResult.Value;
        Assert.NotNull(response);
    }

    #endregion

    #region DeleteTranscription Tests

    [Fact]
    public async Task DeleteTranscription_Success_ReturnsNoContent()
    {
        // Arrange
        var fileId = 1;

        _serviceMock.Setup(s => s.DeleteTranscriptionAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTranscription(fileId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteTranscription_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = 999;

        _serviceMock.Setup(s => s.DeleteTranscriptionAsync(fileId, It.IsAny<User>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTranscription(fileId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = notFoundResult.Value;
        Assert.NotNull(response);
    }

    [Fact]
    public async Task DeleteTranscription_UnauthorizedAccess_ReturnsForbid()
    {
        // Arrange
        var fileId = 1;

        _serviceMock.Setup(s => s.DeleteTranscriptionAsync(fileId, It.IsAny<User>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _controller.DeleteTranscription(fileId);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    #endregion

    #region Helper Methods

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUser.UserId.ToString()),
            new Claim(ClaimTypes.Name, _testUser.Username),
            new Claim(ClaimTypes.Email, _testUser.Email),
            new Claim(ClaimTypes.GivenName, _testUser.FirstName),
            new Claim(ClaimTypes.Surname, _testUser.LastName),
            new Claim(ClaimTypes.Role, _testUser.UserType),
            new Claim("UniversityId", _testUser.UniversityId?.ToString() ?? ""),
            new Claim("isAdmin", (_testUser.IsAdmin ?? false).ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    #endregion
}
