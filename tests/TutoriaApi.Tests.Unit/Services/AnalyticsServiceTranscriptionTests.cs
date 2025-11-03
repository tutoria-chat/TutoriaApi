using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Services;

public class AnalyticsServiceTranscriptionTests
{
    private readonly Mock<IDynamoDbAnalyticsService> _dynamoDbServiceMock;
    private readonly Mock<IModuleRepository> _moduleRepositoryMock;
    private readonly Mock<ICourseRepository> _courseRepositoryMock;
    private readonly Mock<IUniversityRepository> _universityRepositoryMock;
    private readonly Mock<IAIModelRepository> _aiModelRepositoryMock;
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<ILogger<AnalyticsService>> _loggerMock;
    private readonly AnalyticsService _service;

    public AnalyticsServiceTranscriptionTests()
    {
        _dynamoDbServiceMock = new Mock<IDynamoDbAnalyticsService>();
        _moduleRepositoryMock = new Mock<IModuleRepository>();
        _courseRepositoryMock = new Mock<ICourseRepository>();
        _universityRepositoryMock = new Mock<IUniversityRepository>();
        _aiModelRepositoryMock = new Mock<IAIModelRepository>();
        _fileRepositoryMock = new Mock<IFileRepository>();
        _loggerMock = new Mock<ILogger<AnalyticsService>>();

        _service = new AnalyticsService(
            _dynamoDbServiceMock.Object,
            _moduleRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _universityRepositoryMock.Object,
            _aiModelRepositoryMock.Object,
            _fileRepositoryMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetCostAnalysisAsync_IncludesTranscriptionCosts_WhenYouTubeVideosExist()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto
        {
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow
        };

        var modules = CreateTestModules();
        var files = CreateTestTranscriptionFiles();

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);
        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(files);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9.0m, (decimal)result.TranscriptionCostUSD); // 4 videos: $2.50 + $3.00 + $2.00 + $1.50
        Assert.Equal(4, result.TranscriptionVideoCount);
        Assert.Equal(2160, result.TranscriptionTotalDurationSeconds); // 600 + 720 + 480 + 360 seconds
        Assert.Equal(2, result.TranscriptionCostByModule.Count);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_FiltersTranscriptionsByDateRange()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto
        {
            StartDate = DateTime.UtcNow.AddDays(-3), // Only last 3 days
            EndDate = DateTime.UtcNow
        };

        var modules = CreateTestModules();
        var files = CreateTestTranscriptionFiles(); // Has files from 7 days ago

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);

        // Repository filters by date range (should exclude video from 7 days ago)
        var filteredFiles = files
            .Where(f => f.TranscriptedAt >= filters.StartDate && f.TranscriptedAt <= filters.EndDate)
            .ToList();

        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(filteredFiles);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        // Should only include 3 videos within date range (excluding the one from 7 days ago)
        Assert.Equal(7.5m, (decimal)result.TranscriptionCostUSD); // $2.50 + $3.00 + $2.00
        Assert.Equal(3, result.TranscriptionVideoCount);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_ExcludesNonYouTubeFiles()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto();

        var modules = CreateTestModules();
        var files = new List<FileEntity>
        {
            // YouTube video (should be included)
            new FileEntity
            {
                Id = 1,
                Name = "Video 1",
                FileType = "video/youtube",
                FileName = "video1.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 2.50m,
                VideoDurationSeconds = 600,
                TranscriptedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Uploaded PDF (should be excluded)
            new FileEntity
            {
                Id = 2,
                Name = "Document",
                FileType = "application/pdf",
                FileName = "doc.pdf",
                ModuleId = 1,
                SourceType = "upload",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 1.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);
        // Repository should only return YouTube files (filters SourceType == "youtube")
        var youtubeFiles = files.Where(f => f.SourceType == "youtube").ToList();
        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(youtubeFiles);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2.50m, (decimal)result.TranscriptionCostUSD); // Only YouTube video
        Assert.Equal(1, result.TranscriptionVideoCount);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_ExcludesFailedTranscriptions()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto();

        var modules = CreateTestModules();
        var files = new List<FileEntity>
        {
            // Completed transcription (should be included)
            new FileEntity
            {
                Id = 1,
                Name = "Video 1",
                FileType = "video/youtube",
                FileName = "video1.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 2.50m,
                VideoDurationSeconds = 600,
                TranscriptedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Failed transcription (should be excluded)
            new FileEntity
            {
                Id = 2,
                Name = "Video 2",
                FileType = "video/youtube",
                FileName = "video2.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "failed",
                TranscriptionCostUSD = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Pending transcription (should be excluded)
            new FileEntity
            {
                Id = 3,
                Name = "Video 3",
                FileType = "video/youtube",
                FileName = "video3.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "pending",
                TranscriptionCostUSD = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);
        // Repository should only return completed transcriptions (filters TranscriptionStatus == "completed")
        var completedFiles = files.Where(f => f.TranscriptionStatus == "completed").ToList();
        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(completedFiles);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetCostAnalysisAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2.50m, (decimal)result.TranscriptionCostUSD); // Only completed video
        Assert.Equal(1, result.TranscriptionVideoCount);
    }

    [Fact]
    public async Task GetTodayCostAsync_IncludesTranscriptionCosts()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto();

        var modules = CreateTestModules();
        var files = new List<FileEntity>
        {
            new FileEntity
            {
                Id = 1,
                Name = "Today's Video",
                FileType = "video/youtube",
                FileName = "video1.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 3.75m,
                VideoDurationSeconds = 900,
                TranscriptedAt = DateTime.UtcNow.AddHours(-2), // Today
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);
        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(files);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetTodayCostAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3.75m, (decimal)result.TranscriptionCostUSD);
        Assert.Equal(1, result.TranscriptionVideoCount);
        Assert.True(result.ProjectedDailyTranscriptionCost > 0);
    }

    [Fact]
    public async Task GetTodayCostAsync_CalculatesProjectedDailyTranscriptionCost()
    {
        // Arrange
        var userId = 1;
        var userRole = "super_admin";
        int? userUniversityId = null;
        var filters = new AnalyticsFilterDto();

        var modules = CreateTestModules();
        var files = new List<FileEntity>
        {
            new FileEntity
            {
                Id = 1,
                Name = "Morning Video",
                FileType = "video/youtube",
                FileName = "video1.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 2.00m,
                VideoDurationSeconds = 480,
                TranscriptedAt = DateTime.UtcNow.Date.AddHours(6), // 6 AM today
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _moduleRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(modules);
        _fileRepositoryMock.Setup(r => r.GetCompletedTranscriptionsAsync(
                It.IsAny<List<int>>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(files);
        _dynamoDbServiceMock.Setup(s => s.GetModuleAnalyticsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<ChatMessageDto>());
        _aiModelRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AIModel>());

        // Act
        var result = await _service.GetTodayCostAsync(userId, userRole, userUniversityId, filters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2.00m, (decimal)result.TranscriptionCostUSD);
        // Projected cost should be higher than current cost (extrapolated to full day)
        Assert.True(result.ProjectedDailyTranscriptionCost >= result.TranscriptionCostUSD);
    }

    private List<Module> CreateTestModules()
    {
        var course = new Course
        {
            Id = 1,
            Name = "Test Course",
            Code = "CS101",
            UniversityId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return new List<Module>
        {
            new Module
            {
                Id = 1,
                Name = "Module 1",
                Code = "MOD1",
                SystemPrompt = "Test",
                CourseId = 1,
                Course = course,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Module
            {
                Id = 2,
                Name = "Module 2",
                Code = "MOD2",
                SystemPrompt = "Test",
                CourseId = 1,
                Course = course,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    private List<FileEntity> CreateTestTranscriptionFiles()
    {
        return new List<FileEntity>
        {
            new FileEntity
            {
                Id = 1,
                Name = "Video 1",
                FileType = "video/youtube",
                FileName = "video1.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 2.50m,
                VideoDurationSeconds = 600,
                TranscriptedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new FileEntity
            {
                Id = 2,
                Name = "Video 2",
                FileType = "video/youtube",
                FileName = "video2.txt",
                ModuleId = 1,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 3.00m,
                VideoDurationSeconds = 720,
                TranscriptedAt = DateTime.UtcNow.AddDays(-2),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new FileEntity
            {
                Id = 3,
                Name = "Video 3",
                FileType = "video/youtube",
                FileName = "video3.txt",
                ModuleId = 2,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 2.00m,
                VideoDurationSeconds = 480,
                TranscriptedAt = DateTime.UtcNow.AddDays(-3),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Old video outside typical date ranges for filtering tests
            new FileEntity
            {
                Id = 4,
                Name = "Video 4 (Old)",
                FileType = "video/youtube",
                FileName = "video4.txt",
                ModuleId = 2,
                SourceType = "youtube",
                TranscriptionStatus = "completed",
                TranscriptionCostUSD = 1.50m,
                VideoDurationSeconds = 360,
                TranscriptedAt = DateTime.UtcNow.AddDays(-7),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }
}
