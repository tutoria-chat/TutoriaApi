using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Hands jobs to tutoria-worker (extraction, quiz gen, transcription, grading, quiz upload).
/// Jobs:Queue = "sqs" (default): a message on the kind's SQS queue. "postgres": a
/// "WorkerJobs" row with the same JSON (tutoria-api docs/MULTICLOUD.md), claimed by the
/// worker with SKIP LOCKED. The SQS client is only built on the first SQS send: off AWS
/// there is nothing to build (and its credential lookup would wait on EC2 metadata).
/// </summary>
public class SqsMessagingService : ISqsMessagingService
{
    private readonly ILogger<SqsMessagingService> _logger;
    private readonly TutoriaDbContext _db;
    private readonly bool _postgres;
    private readonly string? _extractionQueueUrl;
    private readonly string? _quizGenQueueUrl;
    private readonly string? _transcriptionQueueUrl;
    private readonly string? _gradingQueueUrl;
    private readonly string? _quizUploadQueueUrl;
    private readonly Lazy<IAmazonSQS> _sqsClient;

    public SqsMessagingService(IConfiguration configuration, ILogger<SqsMessagingService> logger, TutoriaDbContext db)
    {
        _logger = logger;
        _db = db;
        _postgres = PostgresStores.JobsInPostgres(configuration);
        _extractionQueueUrl = configuration["Sqs:ExtractionQueueUrl"];
        _quizGenQueueUrl = configuration["Sqs:QuizGenQueueUrl"];
        _transcriptionQueueUrl = configuration["Sqs:TranscriptionQueueUrl"];
        _gradingQueueUrl = configuration["Sqs:GradingQueueUrl"];
        _quizUploadQueueUrl = configuration["Sqs:QuizUploadQueueUrl"];

        var region = configuration["AWS:Region"] ?? "us-east-2";
        var accessKey = configuration["AWS:AccessKeyId"];
        var secretKey = configuration["AWS:SecretAccessKey"];
        _sqsClient = new Lazy<IAmazonSQS>(() =>
            !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
                ? new AmazonSQSClient(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region))
                // IAM role / instance profile (EC2, ECS, Lambda)
                : new AmazonSQSClient(Amazon.RegionEndpoint.GetBySystemName(region)));
    }

    /// <summary>Jobs:Queue=postgres has no queue URLs: every kind has somewhere to go.</summary>
    private bool Missing(string? queueUrl) => !_postgres && string.IsNullOrEmpty(queueUrl);

    /// <inheritdoc />
    public async Task<bool> SendExtractionJobAsync(int fileId, int moduleId)
    {
        if (Missing(_extractionQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:ExtractionQueueUrl is not configured — skipping extraction job for file {FileId}",
                fileId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { file_id = fileId, module_id = moduleId });
        return await SendMessageAsync("extraction", _extractionQueueUrl, body, $"extraction:file:{fileId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendQuizGenerationJobAsync(int moduleId, int count = 50, bool upsert = true)
    {
        if (Missing(_quizGenQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:QuizGenQueueUrl is not configured — skipping quiz-gen job for module {ModuleId}",
                moduleId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { module_id = moduleId, count, upsert });
        return await SendMessageAsync("quiz_gen", _quizGenQueueUrl, body, $"quiz-gen:module:{moduleId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendTranscriptionJobAsync(int fileId, string youtubeUrl, string language)
    {
        if (Missing(_transcriptionQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:TranscriptionQueueUrl is not configured — skipping transcription job for file {FileId}",
                fileId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { file_id = fileId, youtube_url = youtubeUrl, language });
        return await SendMessageAsync("transcription", _transcriptionQueueUrl, body, $"transcription:file:{fileId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendGradingJobAsync(int jobId, int courseId)
    {
        if (Missing(_gradingQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:GradingQueueUrl is not configured — skipping grading job {JobId}",
                jobId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { job_id = jobId, course_id = courseId });
        return await SendMessageAsync("grading", _gradingQueueUrl, body, $"grading:job:{jobId}");
    }

    /// <inheritdoc />
    public async Task<bool> SendQuizUploadJobAsync(int jobId, int courseId)
    {
        if (Missing(_quizUploadQueueUrl))
        {
            _logger.LogWarning(
                "Sqs:QuizUploadQueueUrl is not configured — skipping quiz upload job {JobId}",
                jobId);
            return false;
        }

        var body = JsonSerializer.Serialize(new { job_id = jobId, course_id = courseId });
        return await SendMessageAsync("quiz_upload", _quizUploadQueueUrl, body, $"quiz-upload:job:{jobId}");
    }

    private async Task<bool> SendMessageAsync(string kind, string? queueUrl, string messageBody, string logLabel)
    {
        try
        {
            if (_postgres)
            {
                await PostgresStores.EnqueueJobAsync(_db, kind, messageBody);
                _logger.LogInformation("📨 Job queued in WorkerJobs [{Label}]", logLabel);
                return true;
            }

            var response = await _sqsClient.Value.SendMessageAsync(new SendMessageRequest
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
            _logger.LogError(ex, "❌ Failed to queue job [{Label}]", logLabel);
            return false;
        }
    }
}
