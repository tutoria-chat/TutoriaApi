using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Services;

public class VideoTranscriptionServiceTests
{
    private readonly Mock<ISqsMessagingService> _sqsMock;
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IModuleRepository> _moduleRepositoryMock;
    private readonly Mock<ICourseRepository> _courseRepositoryMock;
    private readonly Mock<ILogger<VideoTranscriptionService>> _loggerMock;
    private readonly VideoTranscriptionService _service;

    public VideoTranscriptionServiceTests()
    {
        _sqsMock = new Mock<ISqsMessagingService>();
        _fileRepositoryMock = new Mock<IFileRepository>();
        _moduleRepositoryMock = new Mock<IModuleRepository>();
        _courseRepositoryMock = new Mock<ICourseRepository>();
        _loggerMock = new Mock<ILogger<VideoTranscriptionService>>();

        _service = new VideoTranscriptionService(
            _sqsMock.Object,
            _fileRepositoryMock.Object,
            _moduleRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region TranscribeYoutubeVideoAsync Tests

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_ModuleNotFound_ThrowsKeyNotFoundException()
    {
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(1))
            .ReturnsAsync((Module?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.TranscribeYoutubeVideoAsync("https://youtube.com/watch?v=test", 1, "pt-br", null, CreateSuperAdmin()));
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_WrongUniversity_ThrowsUnauthorizedAccessException()
    {
        var module = CreateModule(1, courseUniversityId: 2);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(module);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TranscribeYoutubeVideoAsync("https://youtube.com/watch?v=test", 1, "pt-br", null, CreateProfessor(universityId: 1)));
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_Success_CreatesPendingFileAndSendsSqs()
    {
        var module = CreateModule(1);
        var createdFile = CreateFile(99, 1, name: "Custom Name");

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(module);
        _fileRepositoryMock.Setup(r => r.AddAsync(It.IsAny<FileEntity>())).ReturnsAsync(createdFile);
        _sqsMock.Setup(s => s.SendTranscriptionJobAsync(99, It.IsAny<string>(), "pt-br")).ReturnsAsync(true);

        var result = await _service.TranscribeYoutubeVideoAsync(
            "https://youtube.com/watch?v=test123", 1, "pt-br", "Custom Name", CreateSuperAdmin());

        Assert.NotNull(result);
        Assert.Equal(99, result.Id);
        _fileRepositoryMock.Verify(r => r.AddAsync(It.Is<FileEntity>(f =>
            f.SourceType == "youtube" &&
            f.TranscriptionStatus == "pending" &&
            f.SourceUrl == "https://youtube.com/watch?v=test123"
        )), Times.Once);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(99, "https://youtube.com/watch?v=test123", "pt-br"), Times.Once);
    }

    [Fact]
    public async Task TranscribeYoutubeVideoAsync_SuperAdmin_CanAccessAnyModule()
    {
        var module = CreateModule(1, courseUniversityId: 999);
        var createdFile = CreateFile(100, 1);

        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(module);
        _fileRepositoryMock.Setup(r => r.AddAsync(It.IsAny<FileEntity>())).ReturnsAsync(createdFile);
        _sqsMock.Setup(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.TranscribeYoutubeVideoAsync("https://youtube.com/watch?v=test", 1, "pt-br", null, CreateSuperAdmin());

        Assert.NotNull(result);
    }

    #endregion

    #region RetryTranscriptionAsync Tests

    [Fact]
    public async Task RetryTranscriptionAsync_FileNotFound_ThrowsKeyNotFoundException()
    {
        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((FileEntity?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RetryTranscriptionAsync(1, CreateSuperAdmin()));
    }

    [Fact]
    public async Task RetryTranscriptionAsync_NotFailedStatus_ThrowsInvalidOperationException()
    {
        var file = CreateFile(1, 10);
        file.TranscriptionStatus = "completed";
        var module = CreateModule(10);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(10)).ReturnsAsync(module);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RetryTranscriptionAsync(1, CreateSuperAdmin()));

        Assert.Contains("Only failed transcriptions can be retried", ex.Message);
    }

    [Fact]
    public async Task RetryTranscriptionAsync_Success_ResetsToPendingAndSendsSqs()
    {
        var file = CreateFile(1, 10);
        file.TranscriptionStatus = "failed";
        file.SourceUrl = "https://youtube.com/watch?v=abc";
        file.TranscriptLanguage = "pt-br";
        var module = CreateModule(10);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(10)).ReturnsAsync(module);
        _fileRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<FileEntity>())).Returns(Task.CompletedTask);
        _sqsMock.Setup(s => s.SendTranscriptionJobAsync(1, "https://youtube.com/watch?v=abc", "pt-br")).ReturnsAsync(true);

        var result = await _service.RetryTranscriptionAsync(1, CreateSuperAdmin());

        Assert.NotNull(result);
        Assert.Equal("pending", file.TranscriptionStatus);
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.Is<FileEntity>(f => f.TranscriptionStatus == "pending")), Times.Once);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(1, "https://youtube.com/watch?v=abc", "pt-br"), Times.Once);
    }

    #endregion

    #region GetTranscriptionStatusAsync Tests

    [Fact]
    public async Task GetTranscriptionStatusAsync_FileNotFound_ReturnsNull()
    {
        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((FileEntity?)null);

        var result = await _service.GetTranscriptionStatusAsync(1, CreateSuperAdmin());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var file = CreateFile(1, 10);
        var module = CreateModule(10, courseUniversityId: 2);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(10)).ReturnsAsync(module);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetTranscriptionStatusAsync(1, CreateProfessor(universityId: 1)));
    }

    [Fact]
    public async Task GetTranscriptionStatusAsync_AuthorizedUser_ReturnsFile()
    {
        var file = CreateFile(1, 10);
        var module = CreateModule(10);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(10)).ReturnsAsync(module);

        var result = await _service.GetTranscriptionStatusAsync(1, CreateSuperAdmin());

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    #endregion

    #region DeleteTranscriptionAsync Tests

    [Fact]
    public async Task DeleteTranscriptionAsync_FileNotFound_ReturnsFalse()
    {
        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((FileEntity?)null);

        var result = await _service.DeleteTranscriptionAsync(1, CreateSuperAdmin());

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteTranscriptionAsync_Success_SoftDeletesAndReturnsTrue()
    {
        var file = CreateFile(1, 10);
        file.IsActive = true;
        var module = CreateModule(10);

        _fileRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(file);
        _moduleRepositoryMock.Setup(r => r.GetWithDetailsAsync(10)).ReturnsAsync(module);
        _fileRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<FileEntity>())).Returns(Task.CompletedTask);

        var result = await _service.DeleteTranscriptionAsync(1, CreateSuperAdmin());

        Assert.True(result);
        Assert.False(file.IsActive);
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.Is<FileEntity>(f => !f.IsActive)), Times.Once);
    }

    #endregion

    #region Helpers

    private User CreateSuperAdmin() => new()
    {
        UserId = 1, Username = "admin", Email = "admin@test.com",
        FirstName = "Super", LastName = "Admin", UserType = "super_admin", IsActive = true
    };

    private User CreateProfessor(int universityId) => new()
    {
        UserId = 2, Username = "prof", Email = "prof@test.com",
        FirstName = "Test", LastName = "Prof", UserType = "professor",
        UniversityId = universityId, IsAdmin = false, IsActive = true
    };

    private Module CreateModule(int id, int courseUniversityId = 1) => new()
    {
        Id = id, Name = "Test Module", Code = "MOD001", SystemPrompt = "Test", CourseId = 1,
        Course = new Course
        {
            Id = 1, Name = "Course", Code = "CRS001", UniversityId = courseUniversityId,
            University = new University { Id = courseUniversityId, Name = "Uni", Code = "UNI001" }
        }
    };

    private FileEntity CreateFile(int id, int moduleId, string? name = null) => new()
    {
        Id = id, Name = name ?? "Test Video", FileType = "video/youtube",
        ModuleId = moduleId, SourceType = "youtube",
        SourceUrl = "https://youtube.com/watch?v=test123",
        TranscriptionStatus = "completed", IsActive = true, CreatedAt = DateTime.UtcNow
    };

    #endregion
}
