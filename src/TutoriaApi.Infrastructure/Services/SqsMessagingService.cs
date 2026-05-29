using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Sends fire-and-forget job messages to AWS SQS queues.
/// tutoria-worker consumes these and does the heavy processing (extraction, quiz gen).
/// </summary>
public class SqsMessagingService : ISqsMessagingService
{
    private readonly ILogger<SqsMessagingService> _logger;
    private readonly string? _extractionQueueUrl;
    private readonly string? _quizGenQueueUrl;
    private readonly string? _transcriptionQueueUrl;
    private readonly string? _gradingQueueUrl;
    private readonly IAmazonSQS _sqsClient;

    public SqsMessagingService(IConfiguration configuration, ILogger<SqsMessagingService> logger)
    {
        _logger = logger;
        _extractionQueueUrl = configuration["Sqs:ExtractionQueueUrl"];
        _quizGenQueueUrl = configuration["Sqs:QuizGenQueueUrl"];
        _transcriptionQueueUrl = configuration["Sqs:TranscriptionQueueUrl"];
        _gradingQueueUrl = configuration["Sqs:GradingQueueUrl"];

        var region = configuration["AWS:Region"] ?? "us-east-2";
        var accessKey = configuration["AWS:AccessKeyId"];
        var secretKey = configuration["AWS:SecretAccessKey"];

        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            _sqsClient = new AmazonSQSClient(
                accessKey,
                secretKey,
                Amazon.RegionEndpoint.GetBySystemName(region));
        }
        else
        {
            // Fall back to IAM role / instance profile (works on EC2, ECS, Lambda)
            _sqsClient = new AmazonSQSClient(Amazon.RegionEndpoint.GetBySystemName(region));
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendExtractionJobAsync(int fileId, int moduleId)
    {
        if (string.IsNullOrEmpty(_extractionQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:ExtractionQueueUrl is not configured — skipping extraction job for file {FileId}",
                fileId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { file_id = fileId, module_id = moduleId });
        return await SendMessageAsync(_extractionQueueUrl, body, $"extraction:file:{fileId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendQuizGenerationJobAsync(int moduleId, int count = 50, bool upsert = true)
    {
        if (string.IsNullOrEmpty(_quizGenQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:QuizGenQueueUrl is not configured — skipping quiz-gen job for module {ModuleId}",
                moduleId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { module_id = moduleId, count, upsert });
        return await SendMessageAsync(_quizGenQueueUrl, body, $"quiz-gen:module:{moduleId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendTranscriptionJobAsync(int fileId, string youtubeUrl, string language)
    {
        if (string.IsNullOrEmpty(_transcriptionQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:TranscriptionQueueUrl is not configured — skipping transcription job for file {FileId}",
                fileId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { file_id = fileId, youtube_url = youtubeUrl, language });
        return await SendMessageAsync(_transcriptionQueueUrl, body, $"transcription:file:{fileId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendGradingJobAsync(int jobId, int courseId)
    {
        if (string.IsNullOrEmpty(_gradingQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:GradingQueueUrl is not configured — skipping grading job {JobId}",
                jobId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { job_id = jobId, course_id = courseId });
        return await SendMessageAsync(_gradingQueueUrl, body, $"grading:job:{jobId}");
    }

    private async Task<bool> SendMessageAsync(string queueUrl, string messageBody, string logLabel)
    {
        try
        {
            var response = await _sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody,
            });

            _logger.LogInformation(
                "📨 SQS message sent [{Label}] — MessageId: {MessageId}",
                logLabel,
                response.MessageId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send SQS message [{Label}]", logLabel);
            return false;
        }
    }
}
