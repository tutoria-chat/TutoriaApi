// =============================================================================
//  TutorIA Blob Migration Tool
//  Copies all files from Azure Blob Storage to AWS S3, then updates DB records.
//
//  Usage:
//    dotnet run --project tools/BlobMigration -- \
//      --azure-conn "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..." \
//      --azure-container "nonprod" \
//      --s3-bucket "tutoria-files-dev" \
//      --aws-region "us-east-2" \
//      --aws-key "AKIA..." \
//      --aws-secret "..." \
//      --db-conn "Host=...;Port=5432;Database=tutoria_dev;Username=...;Password=..."
//
//  Or with environment variables:
//    AZURE_CONN, AZURE_CONTAINER, S3_BUCKET, AWS_REGION, AWS_ACCESS_KEY,
//    AWS_SECRET_KEY, DB_CONN
//
//  This tool is RERUNNABLE: blobs are overwritten in S3, DB records re-updated.
// =============================================================================

using System.Diagnostics;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Dapper;
using Npgsql;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine();
Console.WriteLine("===================================================================");
Console.WriteLine("  TutorIA Blob Migration: Azure Blob Storage → AWS S3");
Console.WriteLine("  Rerunnable — overwrites existing S3 objects and re-updates DB");
Console.WriteLine("===================================================================");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Parse arguments / environment variables
// ---------------------------------------------------------------------------
var config = ParseArgs(args);

Console.WriteLine($"  Azure container:  {config.AzureContainer}");
Console.WriteLine($"  S3 bucket:        {config.S3Bucket}");
Console.WriteLine($"  AWS region:       {config.AwsRegion}");
Console.WriteLine($"  Database:         {(config.DbConn.Length > 40 ? config.DbConn[..40] + "..." : config.DbConn)}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 2. Initialize clients
// ---------------------------------------------------------------------------
Console.Write("  Connecting to Azure Blob Storage... ");
var azureClient = new BlobServiceClient(config.AzureConn);
var containerClient = azureClient.GetBlobContainerClient(config.AzureContainer);
Console.WriteLine("OK");

Console.Write("  Connecting to AWS S3... ");
var s3Client = new AmazonS3Client(
    config.AwsKey,
    config.AwsSecret,
    RegionEndpoint.GetBySystemName(config.AwsRegion));
Console.WriteLine("OK");

// Ensure S3 bucket exists
Console.Write($"  Ensuring S3 bucket '{config.S3Bucket}' exists... ");
try
{
    await s3Client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = config.S3Bucket });
    Console.WriteLine("exists");
}
catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
{
    await s3Client.PutBucketAsync(new PutBucketRequest
    {
        BucketName = config.S3Bucket,
        BucketRegion = S3Region.FindValue(config.AwsRegion)
    });
    Console.WriteLine("created");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 3. Copy blobs from Azure → S3
// ---------------------------------------------------------------------------
var sw = Stopwatch.StartNew();
var copied = 0;
var failed = new List<string>();
long totalBytes = 0;

Console.WriteLine("  Copying blobs:");
Console.WriteLine("  " + new string('-', 60));

await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
{
    try
    {
        var blobClient = containerClient.GetBlobClient(blobItem.Name);

        // Download from Azure (buffered — S3 needs content length)
        var downloadResponse = await blobClient.DownloadContentAsync();
        var content = downloadResponse.Value.Content.ToArray();
        var contentType = downloadResponse.Value.Details.ContentType ?? "application/octet-stream";

        // Upload to S3 with same key
        using var ms = new MemoryStream(content);
        var putRequest = new PutObjectRequest
        {
            BucketName = config.S3Bucket,
            Key = blobItem.Name,
            InputStream = ms,
            ContentType = contentType
        };

        await s3Client.PutObjectAsync(putRequest);

        totalBytes += content.Length;
        copied++;

        var sizeKb = content.Length / 1024.0;
        Console.WriteLine($"    done  {blobItem.Name,-50} {sizeKb,8:N1} KB");
    }
    catch (Exception ex)
    {
        failed.Add(blobItem.Name);
        Console.WriteLine($"    FAIL  {blobItem.Name,-50} {ex.Message}");
    }
}

Console.WriteLine("  " + new string('-', 60));
Console.WriteLine();
Console.WriteLine($"  Blobs copied:  {copied}");
Console.WriteLine($"  Blobs failed:  {failed.Count}");
Console.WriteLine($"  Total size:    {totalBytes / (1024.0 * 1024.0):N1} MB");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 4. Update database records
// ---------------------------------------------------------------------------
if (!string.IsNullOrEmpty(config.DbConn))
{
    Console.Write("  Connecting to PostgreSQL... ");
    await using var pgConn = new NpgsqlConnection(config.DbConn);
    await pgConn.OpenAsync();
    Console.WriteLine("OK");

    // Build new base URL for S3
    var s3BaseUrl = $"https://{config.S3Bucket}.s3.{config.AwsRegion}.amazonaws.com";

    Console.Write("  Updating Files table (BlobUrl, BlobContainer)... ");

    // Update all files that have a BlobPath:
    //   BlobUrl = s3BaseUrl + "/" + BlobPath
    //   BlobContainer = s3 bucket name
    var rowsUpdated = await pgConn.ExecuteAsync(@"
        UPDATE ""Files""
        SET ""BlobUrl"" = @s3BaseUrl || '/' || ""BlobPath"",
            ""BlobContainer"" = @bucket,
            ""UpdatedAt"" = NOW()
        WHERE ""BlobPath"" IS NOT NULL
          AND ""BlobPath"" != ''",
        new { s3BaseUrl, bucket = config.S3Bucket });

    Console.WriteLine($"{rowsUpdated} rows updated");
    Console.WriteLine();
}
else
{
    Console.WriteLine("  Skipping DB update (no --db-conn provided)");
    Console.WriteLine();
}

// ---------------------------------------------------------------------------
// 5. Summary
// ---------------------------------------------------------------------------
sw.Stop();
Console.WriteLine("===================================================================");
Console.WriteLine($"  Completed in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"  Blobs copied:     {copied}");
Console.WriteLine($"  Blobs failed:     {failed.Count}");
Console.WriteLine($"  Total data:       {totalBytes / (1024.0 * 1024.0):N1} MB");
if (failed.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("  FAILED BLOBS:");
    foreach (var f in failed)
        Console.WriteLine($"    - {f}");
}
Console.WriteLine("===================================================================");
Console.WriteLine();

Environment.Exit(failed.Count > 0 ? 1 : 0);


// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static MigrationConfig ParseArgs(string[] args)
{
    // Parse --key value pairs
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i += 2)
    {
        if (args[i].StartsWith("--"))
            dict[args[i][2..]] = args[i + 1];
    }

    string Get(string key, string envVar, string? fallback = null) =>
        dict.GetValueOrDefault(key)
        ?? Environment.GetEnvironmentVariable(envVar)
        ?? fallback
        ?? throw new InvalidOperationException(
            $"Missing --{key} argument or {envVar} env var");

    return new MigrationConfig
    {
        AzureConn = Get("azure-conn", "AZURE_CONN"),
        AzureContainer = Get("azure-container", "AZURE_CONTAINER"),
        S3Bucket = Get("s3-bucket", "S3_BUCKET"),
        AwsRegion = Get("aws-region", "AWS_REGION", "us-east-2"),
        AwsKey = Get("aws-key", "AWS_ACCESS_KEY"),
        AwsSecret = Get("aws-secret", "AWS_SECRET_KEY"),
        DbConn = Get("db-conn", "DB_CONN", ""),
    };
}

record MigrationConfig
{
    public required string AzureConn { get; init; }
    public required string AzureContainer { get; init; }
    public required string S3Bucket { get; init; }
    public required string AwsRegion { get; init; }
    public required string AwsKey { get; init; }
    public required string AwsSecret { get; init; }
    public required string DbConn { get; init; }
}
