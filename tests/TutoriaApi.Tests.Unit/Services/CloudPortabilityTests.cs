using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public void Ddl_MatchesTutoriaApi()
    {
        // The same tables are created by tutoria-api and tutoria-worker: every quoted
        // identifier (tables, columns, indexes) must match theirs.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tutoria-api")))
            dir = dir.Parent;
        if (dir == null) return; // tutoria-api not checked out next to this repo (CI)
        var python = File.ReadAllText(Path.Combine(dir.FullName, "tutoria-api", "app", "stores", "schema.py"));
        var ddlStart = python.IndexOf("DDL = (", StringComparison.Ordinal);
        var ddlEnd = python.IndexOf("_CLEANUP = (", StringComparison.Ordinal);
        Assert.True(ddlStart >= 0 && ddlEnd > ddlStart, "schema.py layout changed");
        var theirs = Identifiers(python[ddlStart..ddlEnd]);
        var ours = Identifiers(string.Join("\n", PostgresStores.Ddl));
        Assert.Equal(theirs, ours);
        Assert.Contains("734_331_001", python);
        Assert.Equal(734_331_001, PostgresStores.SchemaLock);
    }

    private static SortedSet<string> Identifiers(string sql) =>
        new(Regex.Matches(sql, "\"([A-Za-z_]+)\"").Select(m => m.Groups[1].Value));
}
