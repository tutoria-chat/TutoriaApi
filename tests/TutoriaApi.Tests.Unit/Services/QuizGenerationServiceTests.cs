using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class QuizGenerationServiceTests
{
    private readonly Mock<ISqsMessagingService> _sqsMessagingServiceMock;
    private readonly Mock<ILogger<QuizGenerationService>> _loggerMock;
    private readonly QuizGenerationService _service;

    public QuizGenerationServiceTests()
    {
        _sqsMessagingServiceMock = new Mock<ISqsMessagingService>();
        _loggerMock = new Mock<ILogger<QuizGenerationService>>();

        _service = new QuizGenerationService(
            _sqsMessagingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_SqsSucceeds_ReturnsTrue()
    {
        // Arrange
        var moduleId = 1;
        var count = 50;
        var upsert = true;

        _sqsMessagingServiceMock
            .Setup(s => s.SendQuizGenerationJobAsync(moduleId, count, upsert))
            .ReturnsAsync(true);

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId, count, upsert);

        // Assert
        Assert.True(result);
        _sqsMessagingServiceMock.Verify(
            s => s.SendQuizGenerationJobAsync(moduleId, count, upsert),
            Times.Once);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_SqsFails_ReturnsFalse()
    {
        // Arrange
        var moduleId = 1;

        _sqsMessagingServiceMock
            .Setup(s => s.SendQuizGenerationJobAsync(moduleId, It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_PassesCorrectDefaults_ToSqs()
    {
        // Arrange
        var moduleId = 42;

        _sqsMessagingServiceMock
            .Setup(s => s.SendQuizGenerationJobAsync(moduleId, 50, true))
            .ReturnsAsync(true);

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId);

        // Assert
        Assert.True(result);
        _sqsMessagingServiceMock.Verify(
            s => s.SendQuizGenerationJobAsync(moduleId, 50, true),
            Times.Once);
    }

    [Fact]
    public async Task TriggerQuizGenerationAsync_PassesCustomParams_ToSqs()
    {
        // Arrange
        var moduleId = 7;
        var count = 25;
        var upsert = false;

        _sqsMessagingServiceMock
            .Setup(s => s.SendQuizGenerationJobAsync(moduleId, count, upsert))
            .ReturnsAsync(true);

        // Act
        var result = await _service.TriggerQuizGenerationAsync(moduleId, count, upsert);

        // Assert
        Assert.True(result);
        _sqsMessagingServiceMock.Verify(
            s => s.SendQuizGenerationJobAsync(moduleId, count, upsert),
            Times.Once);
    }
}
