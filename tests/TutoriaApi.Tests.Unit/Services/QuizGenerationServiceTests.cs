using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class QuizGenerationServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<QuizGenerationService>> _loggerMock;
    private readonly QuizGenerationService _service;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public QuizGenerationServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<QuizGenerationService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        // Setup configuration
        _configurationMock.Setup(c => c["AiApi:BaseUrl"]).Returns("http://localhost:8000");

        // Setup HttpClient with mocked handler
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _service = new QuizGenerationService(
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_SuccessfulRequest_ReturnsTrue()
    {
        // Arrange
        var moduleId = 1;
        var count = 50;
        var upsert = true;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"success\",\"message\":\"Generated 50 quizzes\",\"quiz_count\":50}")
            });

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId, count, upsert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_FailedRequest_ReturnsFalse()
    {
        // Arrange
        var moduleId = 1;
        var count = 50;
        var upsert = true;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"detail\":\"AI service error\"}")
            });

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId, count, upsert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_NetworkError_ReturnsFalse()
    {
        // Arrange
        var moduleId = 1;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_Timeout_ReturnsFalse()
    {
        // Arrange
        var moduleId = 1;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_CallsCorrectEndpoint_WithUpsertParameter()
    {
        // Arrange
        var moduleId = 42;
        var count = 25;
        var upsert = true;
        HttpRequestMessage? capturedRequest = null;

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"success\"}")
            });

        // Act
        await _service.TriggerQuizGenerationAsync(moduleId, count, upsert);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains($"/api/v2/modules/{moduleId}/generate-quizzes", capturedRequest.RequestUri?.ToString());
        Assert.Contains($"count={count}", capturedRequest.RequestUri?.ToString());
        Assert.Contains("upsert=true", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public void Constructor_MissingConfiguration_ThrowsException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AiApi:BaseUrl"]).Returns((string?)null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new QuizGenerationService(
                _httpClientFactoryMock.Object,
                configMock.Object,
                _loggerMock.Object));
    }
}
