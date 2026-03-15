// =============================================================================
//  TutorIA Data Migration Tool
//  Copies all data from SQL Server (Azure/RDS) to PostgreSQL (RDS)
//
//  Usage:
//    dotnet run --project tools/DataMigration -- "<sql-server-cs>" "<postgres-cs>"
//
//  Or with environment variables:
//    SOURCE_CONNECTION_STRING="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
//    TARGET_CONNECTION_STRING="Host=...;Port=5432;Database=...;Username=...;Password=..."
//    dotnet run --project tools/DataMigration
//
//  Prerequisites:
//    - Target PostgreSQL must already have the schema (run EF Core migrations first)
//    - Source SQL Server must be accessible from this machine
//
//  This tool is RERUNNABLE: it truncates target tables before inserting.
// =============================================================================

using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;

// Npgsql 6+ requires UTC DateTime for timestamptz columns.
// Enable legacy behavior so Unspecified DateTimes are accepted too (safety net).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine();
Console.WriteLine("===================================================================");
Console.WriteLine("  TutorIA Data Migration: SQL Server -> PostgreSQL");
Console.WriteLine("  Rerunnable - truncates and re-inserts all data");
Console.WriteLine("===================================================================");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Parse connection strings
// ---------------------------------------------------------------------------
var sourceCs = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("SOURCE_CONNECTION_STRING")
      ?? throw new InvalidOperationException(
          "Provide SQL Server connection string as first argument or SOURCE_CONNECTION_STRING env var.\n" +
          "Example: \"Server=tutoria-dev.database.windows.net;Database=TutoriaDb;User Id=admin;Password=xxx;TrustServerCertificate=True\"");

var targetCs = args.Length > 1
    ? args[1]
    : Environment.GetEnvironmentVariable("TARGET_CONNECTION_STRING")
      ?? throw new InvalidOperationException(
          "Provide PostgreSQL connection string as second argument or TARGET_CONNECTION_STRING env var.\n" +
          "Example: \"Host=tutoria-dev.xxx.us-east-2.rds.amazonaws.com;Port=5432;Database=tutoria_dev;Username=tutoria;Password=xxx\"");

// ---------------------------------------------------------------------------
// 2. Define insertion order (parents before children for FK safety)
// ---------------------------------------------------------------------------
var insertionOrder = new[]
{
    // Tier 0 - No FK dependencies
    "AIModels",
    "Plans",
    "Permissions",
    "ApiClients",
    "Universities",

    // Tier 1 - Depends on tier 0
    "Courses",
    "Users",
    "Professors",       // legacy
    "Students",         // legacy
    "SuperAdmins",      // legacy
    "ProviderKeys",
    "RolePermissions",
    "CourseTypeModels",

    // Tier 2 - Depends on tier 1
    "Modules",
    "ProfessorCourses",
    "ProfessorAgents",
    "Consents",
    "AuditLogs",
    "UniversityCourseTypeModels",
    "UniversitySubscriptions",
    "StudentImportJobs",

    // Tier 3 - Depends on tier 2
    "Files",
    "ModuleAccessTokens",
    "Quizzes",
    "Logs",
    "ProfessorAgentTokens",
    "StudentImportRecords",

    // Tier 4 - Depends on tier 3
    "QuizQuestions",
    "QuizAttempts",

    // Tier 5 - Depends on tier 4
    "QuizAnswerOptions",
};

// Tables to never touch
var skipTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "__EFMigrationsHistory",  // EF Core internal - never truncate
    "__RefactorLog",          // SSDT internal
    "sysdiagrams",            // SQL Server internal
};

// ---------------------------------------------------------------------------
// 3. Run migration
// ---------------------------------------------------------------------------
var totalRows = 0;
var migratedTables = 0;
var skippedTables = 0;
var failedTables = new List<string>();
var sw = Stopwatch.StartNew();

try
{
    // --- Open connections ---
    await using var sqlConn = new SqlConnection(sourceCs);
    await using var pgConn = new NpgsqlConnection(targetCs);

    Console.Write("  Connecting to SQL Server source... ");
    await sqlConn.OpenAsync();
    Console.WriteLine("OK");

    Console.Write("  Connecting to PostgreSQL target... ");
    await pgConn.OpenAsync();
    Console.WriteLine("OK");
    Console.WriteLine();

    // --- Discover tables in both databases ---
    var sourceTables = (await sqlConn.QueryAsync<string>(
        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
        "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_TYPE = 'BASE TABLE'"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var targetTables = (await pgConn.QueryAsync<string>(
        "SELECT table_name FROM information_schema.tables " +
        "WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Build final list: predefined order first, then any remaining tables
    var tablesToMigrate = new List<string>();

    foreach (var t in insertionOrder)
    {
        if (sourceTables.Contains(t) && targetTables.Contains(t) && !skipTables.Contains(t))
            tablesToMigrate.Add(t);
    }

    // Append any tables found in BOTH source and target that aren't in predefined order
    foreach (var t in sourceTables)
    {
        if (targetTables.Contains(t) && !skipTables.Contains(t) &&
            !tablesToMigrate.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase)))
        {
            tablesToMigrate.Add(t);
        }
    }

    Console.WriteLine($"  Source tables (SQL Server): {sourceTables.Count}");
    Console.WriteLine($"  Target tables (PostgreSQL): {targetTables.Count}");
    Console.WriteLine($"  Tables to migrate:          {tablesToMigrate.Count}");

    // Warn about tables only in source (not in target)
    var sourceOnly = sourceTables
        .Where(t => !targetTables.Contains(t) && !skipTables.Contains(t))
        .ToList();
    if (sourceOnly.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  WARNING: These source tables have no target equivalent (skipped):");
        foreach (var t in sourceOnly)
            Console.WriteLine($"    - {t}");
    }

    Console.WriteLine();

    // --- Disable triggers during import ---
    // session_replication_role = 'replica' disables all user-defined triggers
    // (our UpdatedAt triggers would overwrite the original timestamps otherwise)
    Console.Write("  Disabling triggers... ");
    await pgConn.ExecuteAsync("SET session_replication_role = 'replica';");
    Console.WriteLine("OK");

    // --- Truncate all target tables at once ---
    Console.Write("  Truncating target tables... ");
    if (tablesToMigrate.Count > 0)
    {
        var truncateList = string.Join(", ", tablesToMigrate.Select(t => $"\"{t}\""));
        await pgConn.ExecuteAsync($"TRUNCATE TABLE {truncateList} CASCADE;");
    }
    Console.WriteLine("OK");
    Console.WriteLine();

    // --- Migrate each table ---
    Console.WriteLine("  Migrating data:");
    Console.WriteLine("  " + new string('-', 60));

    foreach (var table in tablesToMigrate)
    {
        try
        {
            // Read all rows from SQL Server
            var rows = (await sqlConn.QueryAsync($"SELECT * FROM [dbo].[{table}]")).ToList();

            if (rows.Count == 0)
            {
                Console.WriteLine($"    skip  {table,-38} (empty)");
                skippedTables++;
                continue;
            }

            // Get column names from source (first row)
            var sourceColumns = ((IDictionary<string, object>)rows[0]).Keys.ToList();

            // Get column names from target
            var targetCols = (await pgConn.QueryAsync<string>(
                "SELECT column_name FROM information_schema.columns " +
                "WHERE table_name = @table AND table_schema = 'public'",
                new { table })).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Only migrate columns that exist in BOTH source and target
            var columns = sourceColumns
                .Where(c => targetCols.Contains(c))
                .ToList();

            if (columns.Count == 0)
            {
                Console.WriteLine($"    skip  {table,-38} (no matching columns)");
                skippedTables++;
                continue;
            }

            // Warn about mismatched columns
            var droppedCols = sourceColumns.Where(c => !targetCols.Contains(c)).ToList();
            var suffix = droppedCols.Count > 0
                ? $"  (dropped: {string.Join(", ", droppedCols)})"
                : "";

            // Build parameterized INSERT
            var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));
            var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
            var insertSql = $"INSERT INTO \"{table}\" ({colList}) VALUES ({paramList})";

            // Build DynamicParameters for each row with type fixups
            var paramObjects = new List<DynamicParameters>(rows.Count);
            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object>)row;
                var dp = new DynamicParameters();

                foreach (var col in columns)
                {
                    var value = dict.ContainsKey(col) ? dict[col] : null;

                    // Fix DateTime: SQL Server returns Unspecified Kind,
                    // PostgreSQL timestamptz needs UTC
                    if (value is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                        value = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    else if (value is DateTimeOffset dto)
                        value = dto.UtcDateTime;

                    dp.Add(col, value);
                }

                paramObjects.Add(dp);
            }

            // Insert in batches for memory efficiency
            const int batchSize = 500;
            for (var i = 0; i < paramObjects.Count; i += batchSize)
            {
                var batch = paramObjects.Skip(i).Take(batchSize);
                foreach (var dp in batch)
                    await pgConn.ExecuteAsync(insertSql, dp);
            }

            totalRows += rows.Count;
            migratedTables++;
            Console.WriteLine($"    done  {table,-38} {rows.Count,7:N0} rows{suffix}");
        }
        catch (Exception ex)
        {
            failedTables.Add(table);
            Console.WriteLine($"    FAIL  {table,-38} {ex.Message}");
        }
    }

    Console.WriteLine("  " + new string('-', 60));
    Console.WriteLine();

    // --- Reset identity sequences ---
    // After bulk insert with explicit IDs, PostgreSQL sequences are stale.
    // Reset each to MAX(id) so the next INSERT auto-generates correctly.
    Console.Write("  Resetting identity sequences... ");
    await pgConn.ExecuteAsync(@"
        DO $$
        DECLARE
            r RECORD;
            max_val BIGINT;
        BEGIN
            FOR r IN (
                SELECT
                    c.table_name,
                    c.column_name,
                    pg_get_serial_sequence('""' || c.table_name || '""', c.column_name) AS seq
                FROM information_schema.columns c
                WHERE c.column_default LIKE 'nextval%'
                  AND c.table_schema = 'public'
            ) LOOP
                IF r.seq IS NOT NULL THEN
                    EXECUTE format(
                        'SELECT COALESCE(MAX(%I), 0) FROM %I',
                        r.column_name, r.table_name
                    ) INTO max_val;
                    IF max_val > 0 THEN
                        PERFORM setval(r.seq, max_val);
                    END IF;
                END IF;
            END LOOP;
        END $$;
    ");
    Console.WriteLine("OK");

    // --- Re-enable triggers ---
    await pgConn.ExecuteAsync("SET session_replication_role = 'origin';");
    Console.WriteLine("  Triggers re-enabled.");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"  FATAL: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

// ---------------------------------------------------------------------------
// 4. Summary
// ---------------------------------------------------------------------------
sw.Stop();
Console.WriteLine();
Console.WriteLine("===================================================================");
Console.WriteLine($"  Completed in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"  Tables migrated:  {migratedTables}");
Console.WriteLine($"  Tables skipped:   {skippedTables} (empty or no matching columns)");
Console.WriteLine($"  Tables failed:    {failedTables.Count}");
Console.WriteLine($"  Total rows:       {totalRows:N0}");
if (failedTables.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("  FAILED TABLES:");
    foreach (var t in failedTables)
        Console.WriteLine($"    - {t}");
}
Console.WriteLine("===================================================================");
Console.WriteLine();

Environment.Exit(failedTables.Count > 0 ? 1 : 0);
