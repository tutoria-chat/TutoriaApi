// =============================================================================
//  Fix PostgreSQL identity sequences after data migration.
//  Usage: dotnet run --project tools/FixSequences -- "Host=...;Database=..."
// =============================================================================

using Npgsql;

var connStr = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("DB_CONN")
      ?? throw new InvalidOperationException("Provide connection string as first arg or DB_CONN env var");

Console.WriteLine();
Console.WriteLine("  Fixing identity sequences...");
Console.WriteLine();

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

// Find all identity columns and reset their sequences to MAX(id) + 1
await using var cmd = new NpgsqlCommand(@"
    SELECT table_name, column_name
    FROM information_schema.columns
    WHERE is_identity = 'YES' AND table_schema = 'public'
", conn);

var tables = new List<(string table, string column)>();
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        tables.Add((reader.GetString(0), reader.GetString(1)));
}

foreach (var (table, column) in tables)
{
    await using var maxCmd = new NpgsqlCommand($"SELECT COALESCE(MAX(\"{column}\"), 0) FROM \"{table}\"", conn);
    var maxId = Convert.ToInt64(await maxCmd.ExecuteScalarAsync());

    if (maxId > 0)
    {
        var restartVal = maxId + 1;
        await using var resetCmd = new NpgsqlCommand(
            $"ALTER TABLE \"{table}\" ALTER COLUMN \"{column}\" RESTART WITH {restartVal}", conn);
        await resetCmd.ExecuteNonQueryAsync();
        Console.WriteLine($"    {table}.{column}: reset to {restartVal} (max was {maxId})");
    }
    else
    {
        Console.WriteLine($"    {table}.{column}: empty table, skipped");
    }
}

Console.WriteLine();
Console.WriteLine("  Done!");
Console.WriteLine();
