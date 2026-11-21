using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Services;
using TutoriaApi.Web.API.Configuration;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// The switches that let one image run on AWS and on Azure (tutoria-api docs/MULTICLOUD.md):
/// config source, S3-compatible storage, ChatStore and Jobs:Queue.
/// </summary>
public class CloudPortabilityTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static TutoriaDbContext InMemoryDb() =>
        new(new DbContextOptionsBuilder<TutoriaDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // ── config source ────────────────────────────────────────────────────────
    [Fact]
    public void Select_BothSources_RefusesToStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigSource.Select("/tutoria/dev/", "https://x.vault.azure.net/"));
        Assert.Contains("AWS_SSM_PREFIX", ex.Message);
    }

    [Theory]
    [InlineData("/tutoria/dev/", null, ConfigSourceKind.AwsSsm)]
    [InlineData(null, "https://x.vault.azure.net/", ConfigSourceKind.AzureKeyVault)]
    [InlineData(null, null, ConfigSourceKind.Local)]
    [InlineData(" ", "", ConfigSourceKind.Local)]
    public void Select_PicksTheOneThatIsSet(string? ssm, string? vault, ConfigSourceKind expected) =>
        Assert.Equal(expected, ConfigSource.Select(ssm, vault));

    [Fact]
    public void Apply_KeyVaultUrlMustBeHttps()
    {
        var env = new Dictionary<string, string?> { ["AZURE_KEYVAULT_URL"] = "http://x.vault.azure.net/" };
        Assert.Throws<InvalidOperationException>(
            () => ConfigSource.Apply(new ConfigurationBuilder(), k => env.GetValueOrDefault(k)));
    }

    [Fact]
    public void Apply_NeitherSource_AddsNothing()
    {
        var builder = new ConfigurationBuilder();
        var line = ConfigSource.Apply(builder, _ => null);
        Assert.Empty(builder.Sources);
        Assert.Contains("local appsettings", line);
    }

    // ── storage ──────────────────────────────────────────────────────────────
    [Fact]
    public void CustomConfig_IsPathStyleWithRegionAuto()
    {
        var config = StorageEndpoint.CustomConfig("https://acct.r2.cloudflarestorage.com", TimeSpan.FromSeconds(30), 3);
        Assert.True(config.ForcePathStyle);
        Assert.Equal("auto", config.AuthenticationRegion);
        Assert.Equal("https://acct.r2.cloudflarestorage.com/", config.ServiceURL);
    }

    [Fact]
    public void CreateCustomClient_WithoutKeys_Throws()
    {
        var config = Config(("S3:ServiceUrl", "https://acct.r2.cloudflarestorage.com"));
        Assert.Throws<InvalidOperationException>(
            () => StorageEndpoint.CreateCustomClient(config, TimeSpan.FromSeconds(5), 1));
    }

    [Fact]
    public void BlobStorage_CustomEndpoint_StoresPathStyleUrls()
    {
        var config = Config(
            ("S3:BucketName", "tutoria-files-dev"),
            ("S3:ServiceUrl", "https://acct.r2.cloudflarestorage.com/"),
            ("S3:AccessKeyId", "k"),
            ("S3:SecretAccessKey", "s"));
        var storage = new BlobStorageService(config, NullLogger<BlobStorageService>.Instance);
        Assert.Equal("https://acct.r2.cloudflarestorage.com/tutoria-files-dev/a/b.pdf", storage.ObjectUrl("a/b.pdf"));
    }

    // ── store switches ───────────────────────────────────────────────────────
    [Fact]
    public void Switches_DefaultToAws()
    {
        var config = Config();
        Assert.False(PostgresStores.ChatInPostgres(config));
        Assert.False(PostgresStores.JobsInPostgres(config));
    }

    [Fact]
    public void Switches_AcceptPostgresInAnyCase()
    {
        var config = Config(("ChatStore", " Postgres "), ("Jobs:Queue", "POSTGRES"));
        Assert.True(PostgresStores.ChatInPostgres(config));
        Assert.True(PostgresStores.JobsInPostgres(config));
    }

    [Fact]
    public void Switches_UnknownValue_Refuses()
    {
        Assert.Throws<InvalidOperationException>(() => PostgresStores.JobsInPostgres(Config(("Jobs:Queue", "rabbit"))));
        Assert.Throws<InvalidOperationException>(() => PostgresStores.ChatInPostgres(Config(("ChatStore", "redis"))));
    }

    [Fact]
    public async Task Sqs_NoQueueUrl_SkipsWithoutBuildingAClient()
    {
        using var db = InMemoryDb();
        var service = new SqsMessagingService(Config(), NullLogger<SqsMessagingService>.Instance, db);
        Assert.False(await service.SendGradingJobAsync(1, 2));
    }

    [Fact]
    public void ChatHistorySql_FiltersLikeTheGsis()
    {
        var sql = PostgresChatHistory.Sql(PostgresChatHistory.By.Module, hasStart: true, hasEnd: true);
        Assert.Contains("\"ModuleId\" = @key", sql);
        Assert.Contains("\"Ts\" >= @start", sql);
        Assert.Contains("\"Ts\" <= @end", sql);
        Assert.Contains("\"ExpiresAt\" > now()", sql);
        Assert.Contains("ORDER BY \"Ts\" DESC", sql);
        Assert.DoesNotContain("@key", PostgresChatHistory.Sql(null, false, false));
    }

    private static readonly string[] SharedTables = { "ChatHistory", "QuizAttemptAnswers", "WorkerJobs" };

    [Fact]
    public void SharedStoresMigration_MatchesTheEfModel()
    {
        // The migration creates the tables with SQL (they already existed): every table,
        // column and index of the EF model must be in it, and nothing else.
        using var db = new TutoriaDbContext(new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseNpgsql("Host=unused").Options);
        var model = new SortedSet<string>();
        foreach (var entity in db.Model.GetEntityTypes().Where(e => SharedTables.Contains(e.GetTableName())))
        {
            model.Add(entity.GetTableName()!);
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties())
                model.Add(property.GetColumnName(table)!);
            foreach (var index in entity.GetIndexes())
                model.Add(index.GetDatabaseName()!);
        }
        Assert.Contains("IX_WorkerJobs_Waiting", model);
        Assert.Equal(model, Identifiers(TutoriaApi.Infrastructure.Migrations.AddSharedPostgresStores.CreateIfMissing));

        var fileName = db.Model.FindEntityType(typeof(TutoriaApi.Core.Entities.ChatHistoryEntry))!
            .FindProperty(nameof(TutoriaApi.Core.Entities.ChatHistoryEntry.FileName))!;
        Assert.Equal("text", fileName.GetColumnType());
    }

    [Fact]
    public void SharedStoresMigration_MatchesThePythonTestTables()
    {
        // tutoria-api and tutoria-worker create the tables for their tests from
        // tests/postgres_tables.sql: it must stay a copy of this migration's SQL.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tutoria-api")))
            dir = dir.Parent;
        if (dir == null) return; // tutoria-api not checked out next to this repo (CI)
        var python = File.ReadAllText(Path.Combine(dir.FullName, "tutoria-api", "tests", "postgres_tables.sql"));
        Assert.Equal(Normalize(TutoriaApi.Infrastructure.Migrations.AddSharedPostgresStores.CreateIfMissing),
            Normalize(string.Join("\n", python.Split('\n').Where(l => !l.TrimStart().StartsWith("--")))));
    }

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static SortedSet<string> Identifiers(string sql) =>
        new(Regex.Matches(sql, "\"([A-Za-z_]+)\"").Select(m => m.Groups[1].Value));

    [Fact]
    public void PlatformOrigins_ComeFromConfig()
    {
        var config = Config(("Cors:PlatformOrigins", "https://tutoria-ui-dev.x.azurecontainerapps.io/, https://other.example"));
        var origins = TrustedOriginsProvider.PlatformOrigins(config).ToList();
        Assert.Contains("https://tutoria-ui-dev.x.azurecontainerapps.io", origins);
        Assert.Contains("https://other.example", origins);
        Assert.Empty(TrustedOriginsProvider.PlatformOrigins(Config()));
    }
}
