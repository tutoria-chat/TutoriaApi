using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Services;

public class VideoTranscriptionServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IModuleRepository> _moduleRepositoryMock;
    private readonly Mock<ICourseRepository> _courseRepositoryMock;
    private readonly Mock<ILogger<VideoTranscriptionService>> _loggerMock;
    private readonly VideoTranscriptionService _service;

    public VideoTranscriptionServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _fileRepositoryMock = new Mock<IFileRepository>();
        _moduleRepositoryMock = new Mock<IModuleRepository>();
        _courseRepositoryMock = new Mock<ICourseRepository>();
        _loggerMock = new Mock<ILogger<VideoTranscriptionService>>();

        // Setup default configuration
        _configurationMock.Setup(c => c["AiApi:BaseUrl"])
            .Returns("http://localhost:8000");

        _service = new VideoTranscriptionService(
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _fileRepositoryMock.Object,
            _moduleRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region TranscribeYoutubeVideoAsync Tests

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_ModuleNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var youtubeUrl = "https://youtube.com/watch?v=test123";
        var moduleId = 1;
        var user = CreateSuperAdminUser();

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync((Module?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.TranscribeYoutubeVideoAsync(youtubeUrl, moduleId, "pt-br", null, user));
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_RegularProfessorWrongUniversity_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var youtubeUrl = "https://youtube.com/watch?v=test123";
        var moduleId = 1;
        var user = CreateProfessorUser(universityId: 1, isAdmin: false);
        var module = CreateModule(moduleId, courseUniversityId: 2); // Different university

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TranscribeYoutubeVideoAsync(youtubeUrl, moduleId, "pt-br", null, user));
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_SuperAdmin_CanAccessAnyModule()
    {
        // Arrange
        var youtubeUrl = "https://youtube.com/watch?v=test123";
        var moduleId = 1;
        var user = CreateSuperAdminUser();
        var module = CreateModule(moduleId, courseUniversityId: 999);

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        var pythonResponseJson = JsonSerializer.Serialize(new
        {
            file_id = 100,
            status = "completed",
            word_count = 1000,
            source = "youtube_manual",
            language = "pt-br"
        });

        var httpMessageHandlerMock = CreateMockHttpMessageHandler(
            HttpStatusCode.Created,  // Changed from OK to Created (201)
            pythonResponseJson);

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var createdFile = CreateFileEntity(100, moduleId);
        _fileRepositoryMock.Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(createdFile);

        // Act
        var result = await _service.TranscribeYoutubeVideoAsync(
            youtubeUrl, moduleId, "pt-br", "Test Video", user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.Id);
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_PythonApiError_ThrowsInvalidOperationException()
    {
        // Arrange
        var youtubeUrl = "https://youtube.com/watch?v=test123";
        var moduleId = 1;
        var user = CreateSuperAdminUser();
        var module = CreateModule(moduleId);

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        var httpMessageHandlerMock = CreateMockHttpMessageHandler(
            HttpStatusCode.BadRequest,
            "Invalid YouTube URL");

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TranscribeYoutubeVideoAsync(youtubeUrl, moduleId, "pt-br", null, user));

        Assert.Contains("Transcription service returned error", exception.Message);
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_Success_ReturnsFileEntity()
    {
        // Arrange
        var youtubeUrl = "https://youtube.com/watch?v=test123";
        var moduleId = 1;
        var customName = "My Custom Video Name";
        var user = CreateSuperAdminUser();
        var module = CreateModule(moduleId);

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        var pythonResponseJson = JsonSerializer.Serialize(new
        {
            file_id = 100,
            status = "completed",
            word_count = 5000,
            duration_seconds = 3600,
            source = "youtube_auto",
            cost_usd = 0.0,
            language = "pt-br"
        });

        var httpMessageHandlerMock = CreateMockHttpMessageHandler(
            HttpStatusCode.Created,  // Changed from OK to Created (201)
            pythonResponseJson);

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var createdFile = CreateFileEntity(100, moduleId, customName);
        _fileRepositoryMock.Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(createdFile);

        // Act
        var result = await _service.TranscribeYoutubeVideoAsync(
            youtubeUrl, moduleId, "pt-br", customName, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.Id);
        Assert.Equal(customName, result.Name);
        Assert.Equal(moduleId, result.ModuleId);
    }

    #endregion

    #region GetTranscriptionStatusAsync Tests

    [Fact]
    public async Task GetTranscriptionStatusAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var fileId = 1;
        var user = CreateSuperAdminUser();

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _service.GetTranscriptionStatusAsync(fileId, user);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateProfessorUser(universityId: 1, isAdmin: false);
        var file = CreateFileEntity(fileId, moduleId);
        var module = CreateModule(moduleId, courseUniversityId: 2); // Different university

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetTranscriptionStatusAsync(fileId, user));
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_AuthorizedUser_ReturnsFile()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        var module = CreateModule(moduleId);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act
        var result = await _service.GetTranscriptionStatusAsync(fileId, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileId, result.Id);
    }

    #endregion

    #region GetTranscriptTextAsync Tests

    [Fact]
    public async Task GetTranscriptTextAsync_NoTranscriptText_ReturnsNull()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        file.TranscriptText = null; // No transcript
        var module = CreateModule(moduleId);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act
        var result = await _service.GetTranscriptTextAsync(fileId, user);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTranscriptTextAsync_WithTranscript_ReturnsFile()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        file.TranscriptText = "This is the full transcript text...";
        var module = CreateModule(moduleId);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act
        var result = await _service.GetTranscriptTextAsync(fileId, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileId, result.Id);
        Assert.NotNull(result.TranscriptText);
    }

    #endregion

    #region RetryTranscriptionAsync Tests

    [Fact]
    public async Task RetryTranscriptionAsync_FileNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var fileId = 1;
        var user = CreateSuperAdminUser();

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync((FileEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RetryTranscriptionAsync(fileId, user));
    }

    [Fact]
    public async Task RetryTranscriptionAsync_NotFailedStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        file.TranscriptionStatus = "completed"; // Not failed
        var module = CreateModule(moduleId);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RetryTranscriptionAsync(fileId, user));

        Assert.Contains("Only failed transcriptions can be retried", exception.Message);
    }

    [Fact]
    public async Task RetryTranscriptionAsync_Success_ReturnsUpdatedFile()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        file.TranscriptionStatus = "failed";
        var module = CreateModule(moduleId);
        var updatedFile = CreateFileEntity(fileId, moduleId);
        updatedFile.TranscriptionStatus = "completed";

        // Service calls GetByIdAsync twice - first to check status, then to get updated file
        _fileRepositoryMock.SetupSequence(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file)              // First call: returns file with status "failed"
            .ReturnsAsync(updatedFile);      // Second call: returns updated file with status "completed"

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        var httpMessageHandlerMock = CreateMockHttpMessageHandler(
            HttpStatusCode.OK,
            "{}");

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _service.RetryTranscriptionAsync(fileId, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("completed", result.TranscriptionStatus);
    }

    #endregion

    #region DeleteTranscriptionAsync Tests

    [Fact]
    public async Task DeleteTranscriptionAsync_FileNotFound_ReturnsFalse()
    {
        // Arrange
        var fileId = 1;
        var user = CreateSuperAdminUser();

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _service.DeleteTranscriptionAsync(fileId, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteTranscriptionAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateProfessorUser(universityId: 1, isAdmin: false);
        var file = CreateFileEntity(fileId, moduleId);
        var module = CreateModule(moduleId, courseUniversityId: 2);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DeleteTranscriptionAsync(fileId, user));
    }

    [Fact]
    public async Task DeleteTranscriptionAsync_Success_SoftDeletesAndReturnsTrue()
    {
        // Arrange
        var fileId = 1;
        var moduleId = 10;
        var user = CreateSuperAdminUser();
        var file = CreateFileEntity(fileId, moduleId);
        file.IsActive = true;
        var module = CreateModule(moduleId);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(moduleId))
            .ReturnsAsync(module);
        _fileRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<FileEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteTranscriptionAsync(fileId, user);

        // Assert
        Assert.True(result);
        Assert.False(file.IsActive);
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.Is<FileEntity>(f => !f.IsActive)), Times.Once);
    }

    #endregion

    #region Helper Methods

    private User CreateSuperAdminUser()
    {
        return new User
        {
            UserId = 1,
            Username = "superadmin",
            Email = "admin@test.com",
            FirstName = "Super",
            LastName = "Admin",
            UserType = "super_admin",
            IsActive = true
        };
    }

    private User CreateProfessorUser(int universityId, bool isAdmin)
    {
        return new User
        {
            UserId = 2,
            Username = "professor",
            Email = "professor@test.com",
            FirstName = "Test",
            LastName = "Professor",
            UserType = "professor",
            UniversityId = universityId,
            IsAdmin = isAdmin,
            IsActive = true
        };
    }

    private Module CreateModule(int moduleId, int courseUniversityId = 1)
    {
        return new Module
        {
            Id = moduleId,
            Name = "Test Module",
            Code = "MOD001",
            SystemPrompt = "Test prompt",
            Semester = 1,
            Year = 2024,
            CourseId = 1,
            Course = new Course
            {
                Id = 1,
                Name = "Test Course",
                Code = "CRS001",
                UniversityId = courseUniversityId,
                University = new University
                {
                    Id = courseUniversityId,
                    Name = "Test University",
                    Code = "UNI001"
                }
            }
        };
    }

    private FileEntity CreateFileEntity(int fileId, int moduleId, string? name = null)
    {
        return new FileEntity
        {
            Id = fileId,
            Name = name ?? "Test Video",
            FileType = "youtube_transcript",
            ModuleId = moduleId,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test123",
            TranscriptionStatus = "completed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return handlerMock;
    }

    #endregion
}
