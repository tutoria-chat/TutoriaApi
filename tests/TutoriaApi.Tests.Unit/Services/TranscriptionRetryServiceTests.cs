using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Tests.Unit.Services;

public class TranscriptionRetryServiceTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<ISqsMessagingService> _sqsMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TranscriptionRetryService>> _loggerMock;
    private readonly TranscriptionRetryService _service;

    public TranscriptionRetryServiceTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _sqsMock = new Mock<ISqsMessagingService>();
        _loggerMock = new Mock<ILogger<TranscriptionRetryService>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TranscriptionRetry:DelayBetweenRetriesMs", "0" },
                { "TranscriptionRetry:MaxRetryAgeHours", "72" }
            })
            .Build();

        _service = new TranscriptionRetryService(
            _fileRepositoryMock.Object,
            _sqsMock.Object,
            _configuration,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_NoFailedFiles_ReturnsZero()
    {
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(new List<FileEntity>());

        var result = await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal(0, result);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_AllSucceed_ReturnsCount()
    {
        var files = CreateFailedFiles(3);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(files);
        _fileRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<FileEntity>()))
            .Returns(Task.CompletedTask);
        _sqsMock
            .Setup(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal(3, result);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.Is<FileEntity>(f => f.TranscriptionStatus == "pending")), Times.Exactly(3));
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_SqsReturnsFalse_CountedAsFailure()
    {
        var files = CreateFailedFiles(3);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(files);
        _fileRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<FileEntity>()))
            .Returns(Task.CompletedTask);

        // First succeeds, others fail
        _sqsMock.SetupSequence(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false)
            .ReturnsAsync(false);

        var result = await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_MissingSourceUrl_SkipsFile()
    {
        var files = new List<FileEntity>
        {
            new() { Id = 1, SourceUrl = null, TranscriptionStatus = "failed", IsActive = true,
                    Name = "No URL", FileType = "video/youtube", ModuleId = 1, SourceType = "youtube", CreatedAt = DateTime.UtcNow }
        };
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(files);

        var result = await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal(0, result);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<FileEntity>()), Times.Never);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_ResetsStatusToPendingBeforeSqs()
    {
        var file = new FileEntity
        {
            Id = 42, SourceUrl = "https://youtube.com/watch?v=abc", TranscriptLanguage = "en",
            TranscriptionStatus = "failed", IsActive = true, Name = "Vid", FileType = "video/youtube",
            ModuleId = 1, SourceType = "youtube", CreatedAt = DateTime.UtcNow
        };
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(new List<FileEntity> { file });
        _fileRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<FileEntity>()))
            .Returns(Task.CompletedTask);
        _sqsMock
            .Setup(s => s.SendTranscriptionJobAsync(42, "https://youtube.com/watch?v=abc", "en"))
            .ReturnsAsync(true);

        await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal("pending", file.TranscriptionStatus);
        _fileRepositoryMock.Verify(r => r.UpdateAsync(It.Is<FileEntity>(f => f.TranscriptionStatus == "pending")), Times.Once);
        _sqsMock.Verify(s => s.SendTranscriptionJobAsync(42, "https://youtube.com/watch?v=abc", "en"), Times.Once);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_SqsException_HandledGracefully()
    {
        var files = CreateFailedFiles(1);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(files);
        _fileRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<FileEntity>()))
            .Returns(Task.CompletedTask);
        _sqsMock
            .Setup(s => s.SendTranscriptionJobAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SQS unavailable"));

        var result = await _service.RetryFailedTranscriptionsAsync();

        Assert.Equal(0, result);
    }

    private static List<FileEntity> CreateFailedFiles(int count)
    {
        var files = new List<FileEntity>();
        for (int i = 1; i <= count; i++)
        {
            files.Add(new FileEntity
            {
                Id = i, Name = $"Failed Video {i}", FileType = "video/youtube",
                ModuleId = 1, SourceType = "youtube",
                SourceUrl = $"https://youtube.com/watch?v=test{i}",
                TranscriptLanguage = "pt-br",
                TranscriptionStatus = "failed", IsActive = true,
                CreatedAt = DateTime.UtcNow.AddHours(-24)
            });
        }
        return files;
    }
}
