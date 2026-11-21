using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TutoriaApi.Infrastructure.Data;

/// <summary>
/// The Postgres tables that replace DynamoDB and SQS (tutoria-api docs/MULTICLOUD.md):
/// "ChatHistory" (ChatStore=postgres), "QuizAttemptAnswers", and "WorkerJobs"
/// (Jobs:Queue=postgres). They are shared with tutoria-api and tutoria-worker, which use
/// them with plain SQL; their schema is this API's, in the EF model
/// (TutoriaDbContext.ConfigureSharedStores) and migrations (AddSharedPostgresStores).
/// </summary>
public static class PostgresStores
{
    public const string ChatStoreKey = "ChatStore";
    public const string JobQueueKey = "Jobs:Queue";

    public static bool ChatInPostgres(IConfiguration configuration) =>
        Choice(configuration, ChatStoreKey, "dynamodb", "postgres") == "postgres";

    public static bool JobsInPostgres(IConfiguration configuration) =>
        Choice(configuration, JobQueueKey, "sqs", "postgres") == "postgres";

    /// <summary>The value, lower-cased; an unknown one refuses (a typo must not fall back to AWS).</summary>
    public static string Choice(IConfiguration configuration, string key, string defaultValue, string other)
    {
        var value = (configuration[key] ?? defaultValue).Trim().ToLowerInvariant();
        if (value.Length == 0) value = defaultValue;
        if (value != defaultValue && value != other)
            throw new InvalidOperationException($"{key} must be '{defaultValue}' or '{other}', is '{value}'");
        return value;
    }

    /// <summary>A job for tutoria-worker: the same JSON the SQS message carried.</summary>
    public static Task<int> EnqueueJobAsync(TutoriaDbContext db, string kind, string payloadJson, CancellationToken ct = default) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "WorkerJobs" ("Kind", "Payload") VALUES ({kind}, {payloadJson})""", ct);

    /// <summary>An open connection of the context (opened here when it was closed).</summary>
    public static async Task<DbConnection> OpenConnectionAsync(TutoriaDbContext db, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        return connection;
    }
}
