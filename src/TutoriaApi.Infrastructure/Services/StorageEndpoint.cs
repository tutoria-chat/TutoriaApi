using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// File storage is S3 or another S3-compatible store (Cloudflare R2 where there is no
/// AWS). S3:ServiceUrl set: that endpoint, path-style, signing region "auto",
/// with S3:AccessKeyId / S3:SecretAccessKey. Unset: AWS S3 with the default
/// credential chain, as on ECS. The bucket is S3:BucketName either way.
/// </summary>
public static class StorageEndpoint
{
    public const string ServiceUrlKey = "S3:ServiceUrl";
    public const string AccessKeyIdKey = "S3:AccessKeyId";
    public const string SecretAccessKeyKey = "S3:SecretAccessKey";

    public static bool IsCustom(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[ServiceUrlKey]);

    /// <summary>The client for S3:ServiceUrl. Static credentials, so nothing is looked up on the network.</summary>
    public static AmazonS3Client CreateCustomClient(IConfiguration configuration, TimeSpan timeout, int maxRetries)
    {
        var serviceUrl = configuration[ServiceUrlKey]?.Trim();
        if (string.IsNullOrEmpty(serviceUrl))
            throw new InvalidOperationException($"{ServiceUrlKey} not configured");
        var accessKeyId = configuration[AccessKeyIdKey];
        var secretAccessKey = configuration[SecretAccessKeyKey];
        if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
            throw new InvalidOperationException($"{ServiceUrlKey} is set, so {AccessKeyIdKey} and {SecretAccessKeyKey} are required");

        return new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), CustomConfig(serviceUrl, timeout, maxRetries));
    }

    public static AmazonS3Config CustomConfig(string serviceUrl, TimeSpan timeout, int maxRetries) => new()
    {
        ServiceURL = serviceUrl,
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
        Timeout = timeout,
        MaxErrorRetry = maxRetries,
        RetryMode = RequestRetryMode.Standard,
        // The SDK's default flexible checksums (trailing CRC on every upload) aren't
        // something every S3-compatible store accepts; send them only where S3 requires.
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
    };
}
