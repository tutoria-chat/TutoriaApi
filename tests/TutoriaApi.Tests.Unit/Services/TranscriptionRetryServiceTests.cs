using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;
using FileEntity = TutoriaApi.Core.Entities.File;
using System.Collections.Generic;

namespace TutoriaApi.Tests.Unit.Services;

public class TranscriptionRetryServiceTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TranscriptionRetryService>> _loggerMock;
    private readonly TranscriptionRetryService _service;

    public TranscriptionRetryServiceTests()
    {
        _fileRepositoryMock = new Mock<IFileRepository>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<TranscriptionRetryService>>();

        // Setup in-memory configuration
        var configurationData = new Dictionary<string, string?>
        {
            { "AiApi:BaseUrl", "http://localhost:8000" },
            { "TranscriptionRetry:DelayBetweenRetriesMs", "2000" },
            { "TranscriptionRetry:MaxRetryAgeHours", "72" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        _service = new TranscriptionRetryService(
            _fileRepositoryMock.Object,
            _httpClientFactoryMock.Object,
            _configuration,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_NoFailedFiles_ReturnsZero()
    {
        // Arrange
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(new List<FileEntity>());

        // Act
        var result = await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.Equal(0, result);
        _httpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_SuccessfulRetries_ReturnsSuccessCount()
    {
        // Arrange
        var failedFiles = CreateTestFailedFiles(3);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(failedFiles);

        // Mock successful HTTP responses for all 3 calls
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"completed\"}")
            });

        // Return a new HttpClient instance for each call to CreateClient()
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8000")
            });

        // Act
        var result = await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.Equal(3, result); // All 3 should succeed
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_SomeFailures_ReturnsOnlySuccessCount()
    {
        // Arrange
        var failedFiles = CreateTestFailedFiles(3);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(failedFiles);

        // Mock HTTP handler to succeed for first file, fail for second and third
        var handlerMock = new Mock<HttpMessageHandler>();
        var sequence = handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );

        // First call succeeds
        sequence.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"status\":\"completed\"}")
        });

        // Second call fails (400 Bad Request)
        sequence.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("{\"error\":\"Invalid request\"}")
        });

        // Third call fails (500 Internal Server Error)
        sequence.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("{\"error\":\"Server error\"}")
        });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.Equal(1, result); // Only 1 should succeed
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_CallsCorrectPythonApiEndpoint()
    {
        // Arrange
        var failedFiles = CreateTestFailedFiles(1);
        var fileId = failedFiles[0].Id;

        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(failedFiles);

        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"completed\"}")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal($"/api/v2/transcription/retry/{fileId}", capturedRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_HandlesHttpRequestException()
    {
        // Arrange
        var failedFiles = CreateTestFailedFiles(1);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(failedFiles);

        // Mock HTTP handler to throw exception
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.Equal(0, result); // Should return 0 successes
        // Should not throw exception - should handle gracefully
    }

    [Fact]
    public async Task RetryFailedTranscriptionsAsync_HandlesTaskCanceledException()
    {
        // Arrange
        var failedFiles = CreateTestFailedFiles(1);
        _fileRepositoryMock
            .Setup(r => r.GetFailedYoutubeTranscriptionsFromLast72HoursAsync())
            .ReturnsAsync(failedFiles);

        // Mock HTTP handler to throw timeout exception
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.RetryFailedTranscriptionsAsync();

        // Assert
        Assert.Equal(0, result); // Should return 0 successes
        // Should not throw exception - should handle gracefully
    }

    [Fact]
    public void RetryFailedTranscriptionsAsync_ConfigurationMissing_ThrowsException()
    {
        // Arrange - configuration without AiApi:BaseUrl
        var configurationData = new Dictionary<string, string?>
        {
            { "TranscriptionRetry:DelayBetweenRetriesMs", "2000" },
            { "TranscriptionRetry:MaxRetryAgeHours", "72" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new TranscriptionRetryService(
                _fileRepositoryMock.Object,
                _httpClientFactoryMock.Object,
                config,
                _loggerMock.Object
            )
        );
    }

    private List<FileEntity> CreateTestFailedFiles(int count)
    {
        var files = new List<FileEntity>();
        var module = new Module
        {
            Id = 1,
            Name = "Test Module",
            Code = "MOD1",
            SystemPrompt = "Test prompt",
            CourseId = 1
        };

        for (int i = 1; i <= count; i++)
        {
            files.Add(new FileEntity
            {
                Id = i,
                Name = $"Failed Video {i}",
                FileType = "video/youtube",
                FileName = $"video{i}.txt",
                ModuleId = 1,
                Module = module,
                SourceType = "youtube",
                SourceUrl = $"https://youtube.com/watch?v=test{i}",
                TranscriptionStatus = "failed",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddHours(-24),
                UpdatedAt = DateTime.UtcNow.AddHours(-24)
            });
        }

        return files;
    }

    private Mock<HttpMessageHandler> CreateHttpMessageHandlerMock(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("{\"status\":\"completed\"}")
            });

        return handlerMock;
    }
}
