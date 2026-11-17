using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _region;
    private readonly string? _serviceUrl;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;

        _bucketName = configuration["S3:BucketName"]
            ?? throw new InvalidOperationException("S3:BucketName not configured");
        _region = configuration["S3:Region"]
            ?? configuration["AWS:Region"]
            ?? "us-east-2";

        _serviceUrl = configuration[StorageEndpoint.ServiceUrlKey]?.Trim().TrimEnd('/');

        try
        {
            if (StorageEndpoint.IsCustom(configuration))
            {
                // S3-compatible store (R2 on Azure): its own keys, never the AWS chain.
                _s3Client = StorageEndpoint.CreateCustomClient(configuration, TimeSpan.FromSeconds(30), 3);
                return;
            }

            var accessKeyId = configuration["AWS:AccessKeyId"];
            var secretAccessKey = configuration["AWS:SecretAccessKey"];
            _s3Client = !string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey)
                ? new AmazonS3Client(accessKeyId, secretAccessKey, RegionEndpoint.GetBySystemName(_region))
                // Default credential chain (IAM role, env vars, etc.)
                : new AmazonS3Client(RegionEndpoint.GetBySystemName(_region));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize S3 storage service");
            throw new InvalidOperationException($"Invalid S3 configuration: {ex.Message}", ex);
        }

        // Once per process, not per request: this service is scoped, and a network
        // call in its constructor used to run (and block) on every request using it.
        if (CheckedBuckets.TryAdd(_bucketName, 0))
        {
            try
            {
                EnsureBucketExistsAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                CheckedBuckets.TryRemove(_bucketName, out _);
                _logger.LogError(ex, "Could not check S3 bucket {BucketName}; file operations may fail", _bucketName);
            }
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> CheckedBuckets = new();

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            await _s3Client.GetBucketLocationAsync(new GetBucketLocationRequest
            {
                BucketName = _bucketName
            });
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _s3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _bucketName,
                BucketRegion = S3Region.FindValue(_region)
            });
            _logger.LogInformation("Created S3 bucket: {BucketName}", _bucketName);
        }
    }

    public string GenerateBlobPath(int universityId, int courseId, int moduleId, string filename)
    {
        // Generate unique filename to avoid conflicts
        var extension = Path.GetExtension(filename);
        var uniqueFilename = $"{Guid.NewGuid()}{extension}";

        return $"universities/{universityId}/courses/{courseId}/modules/{moduleId}/{uniqueFilename}";
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType)
    {
        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = blobPath,
                InputStream = fileStream,
                ContentType = contentType ?? "application/octet-stream"
            };

            await _s3Client.PutObjectAsync(putRequest);

            return ObjectUrl(blobPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to S3: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to upload file", ex);
        }
    }

    public async Task<bool> DeleteFileAsync(string blobPath)
    {
        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = blobPath
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);

            // S3 DeleteObject doesn't fail if key doesn't exist — always returns 204
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete S3 object: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to delete file", ex);
        }
    }

    public string GetDownloadUrl(string blobPath, int expiresInHours = 1)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = blobPath,
                Expires = DateTime.UtcNow.AddHours(expiresInHours),
                Verb = HttpVerb.GET
            };

            return _s3Client.GetPreSignedURL(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for S3 object: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to generate download URL", ex);
        }
    }

    public async Task<byte[]?> GetFileContentAsync(string blobPath)
    {
        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = blobPath
            };

            using var response = await _s3Client.GetObjectAsync(getRequest);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("S3 object not found: {BlobPath}", blobPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file content from S3: {BlobPath}", blobPath);
            return null;
        }
    }

    /// <summary>The URL stored next to an object (Files.BlobUrl): AWS virtual-hosted, or {S3:ServiceUrl}/{bucket}/{key}.</summary>
    public string ObjectUrl(string blobPath) => string.IsNullOrEmpty(_serviceUrl)
        ? $"https://{_bucketName}.s3.{_region}.amazonaws.com/{blobPath}"
        : $"{_serviceUrl}/{_bucketName}/{blobPath}";
}
