using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Enqueues quiz generation jobs via SQS for tutoria-worker to process asynchronously.
/// </summary>
public class QuizGenerationService : IQuizGenerationService
{
    private readonly ISqsMessagingService _sqsMessagingService;
    private readonly ILogger<QuizGenerationService> _logger;

    public QuizGenerationService(
        ISqsMessagingService sqsMessagingService,
        ILogger<QuizGenerationService> logger)
    {
        _sqsMessagingService = sqsMessagingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TriggerQuizGenerationAsync(int moduleId, int count = 50, bool upsert = true)
    {
        _logger.LogInformation(
            "📝 Enqueueing quiz generation job for module {ModuleId} (count: {Count}, upsert: {Upsert})",
            moduleId,
            count,
            upsert);

        return await _sqsMessagingService.SendQuizGenerationJobAsync(moduleId, count, upsert);
    }
}
