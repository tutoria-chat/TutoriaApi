using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using Xunit;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Repositories;

public class FileRepositoryTests : IDisposable
{
    private readonly TutoriaDbContext _context;
    private readonly FileRepository _repository;

    public FileRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TutoriaDbContext(options);
        _repository = new FileRepository(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var university = new University
        {
            Id = 1,
            Name = "Test University",
            Code = "TU"
        };

        var course = new Course
        {
            Id = 1,
            Name = "Test Course",
            Code = "CS101",
            UniversityId = 1,
            University = university
        };

        var module = new Module
        {
            Id = 1,
            Name = "Test Module",
            Code = "MOD1",
            SystemPrompt = "Test prompt",
            CourseId = 1,
            Course = course
        };

        // Failed YouTube transcription from 24 hours ago (should be included)
        var failedFile1 = new FileEntity
        {
            Id = 1,
            Name = "Failed Video 1",
            FileType = "video/youtube",
            FileName = "video1.txt",
            ModuleId = 1,
            Module = module,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test1",
            TranscriptionStatus = "failed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-24),
            UpdatedAt = DateTime.UtcNow.AddHours(-24)
        };

        // Failed YouTube transcription from 48 hours ago (should be included)
        var failedFile2 = new FileEntity
        {
            Id = 2,
            Name = "Failed Video 2",
            FileType = "video/youtube",
            FileName = "video2.txt",
            ModuleId = 1,
            Module = module,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test2",
            TranscriptionStatus = "failed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-48),
            UpdatedAt = DateTime.UtcNow.AddHours(-48)
        };

        // Failed YouTube transcription from 80 hours ago (should NOT be included - outside 72h window)
        var failedFile3 = new FileEntity
        {
            Id = 3,
            Name = "Failed Video 3",
            FileType = "video/youtube",
            FileName = "video3.txt",
            ModuleId = 1,
            Module = module,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test3",
            TranscriptionStatus = "failed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-80),
            UpdatedAt = DateTime.UtcNow.AddHours(-80)
        };

        // Completed YouTube transcription (should NOT be included)
        var completedFile = new FileEntity
        {
            Id = 4,
            Name = "Completed Video",
            FileType = "video/youtube",
            FileName = "video4.txt",
            ModuleId = 1,
            Module = module,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test4",
            TranscriptionStatus = "completed",
            TranscriptText = "Sample transcript",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-24),
            UpdatedAt = DateTime.UtcNow.AddHours(-24)
        };

        // Failed upload (not YouTube, should NOT be included)
        var failedUpload = new FileEntity
        {
            Id = 5,
            Name = "Failed Upload",
            FileType = "application/pdf",
            FileName = "document.pdf",
            ModuleId = 1,
            Module = module,
            SourceType = "upload",
            TranscriptionStatus = "failed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-24),
            UpdatedAt = DateTime.UtcNow.AddHours(-24)
        };

        // Failed YouTube transcription but inactive (should NOT be included)
        var inactiveFile = new FileEntity
        {
            Id = 6,
            Name = "Inactive Failed Video",
            FileType = "video/youtube",
            FileName = "video6.txt",
            ModuleId = 1,
            Module = module,
            SourceType = "youtube",
            SourceUrl = "https://youtube.com/watch?v=test6",
            TranscriptionStatus = "failed",
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddHours(-24),
            UpdatedAt = DateTime.UtcNow.AddHours(-24)
        };

        _context.Universities.Add(university);
        _context.Courses.Add(course);
        _context.Modules.Add(module);
        _context.Files.AddRange(failedFile1, failedFile2, failedFile3, completedFile, failedUpload, inactiveFile);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetFailedYoutubeTranscriptionsFromLast72HoursAsync_ReturnsOnlyFailedYouTubeVideosWithin72Hours()
    {
        // Act
        var result = await _repository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Should only return the 2 failed videos from 24h and 48h ago
        Assert.All(result, file =>
        {
            Assert.Equal("youtube", file.SourceType);
            Assert.Equal("failed", file.TranscriptionStatus);
            Assert.True(file.IsActive);
            Assert.True(file.CreatedAt >= DateTime.UtcNow.AddHours(-72));
        });
    }

    [Fact]
    public async Task GetFailedYoutubeTranscriptionsFromLast72HoursAsync_ReturnsFilesOrderedByCreatedAt()
    {
        // Act
        var result = await _repository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // Should be ordered by CreatedAt (oldest first)
        Assert.True(result[0].CreatedAt <= result[1].CreatedAt);
    }

    [Fact]
    public async Task GetFailedYoutubeTranscriptionsFromLast72HoursAsync_IncludesModuleNavigation()
    {
        // Act
        var result = await _repository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

        // Assert
        Assert.NotNull(result);
        Assert.All(result, file =>
        {
            Assert.NotNull(file.Module);
            Assert.Equal("Test Module", file.Module.Name);
        });
    }

    [Fact]
    public async Task GetFailedYoutubeTranscriptionsFromLast72HoursAsync_ReturnsEmptyListWhenNoFailedVideos()
    {
        // Arrange - Clear all files
        _context.Files.RemoveRange(_context.Files);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFailedYoutubeTranscriptionsFromLast72HoursAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByModuleIdAsync_ReturnsFilesForModule()
    {
        // Act
        var result = await _repository.GetByModuleIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Count); // All 6 test files belong to module 1
        Assert.All(result, file => Assert.Equal(1, file.ModuleId));
    }

    [Fact]
    public async Task ExistsByBlobNameAsync_ReturnsTrueWhenBlobExists()
    {
        // Arrange
        var file = new FileEntity
        {
            Id = 100,
            Name = "Blob Test",
            FileType = "application/pdf",
            FileName = "test.pdf",
            BlobPath = "unique-blob-path-123",
            ModuleId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Files.Add(file);
        await _context.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsByBlobNameAsync("unique-blob-path-123");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByBlobNameAsync_ReturnsFalseWhenBlobDoesNotExist()
    {
        // Act
        var exists = await _repository.ExistsByBlobNameAsync("non-existent-blob-path");

        // Assert
        Assert.False(exists);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
