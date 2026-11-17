using System.Data.Common;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Data;

/// <summary>
/// Chat history from "ChatHistory" (ChatStore=postgres), the Postgres form of the
/// DynamoDB ChatMessages table: same rows, read newest first like the GSI queries.
/// Written by tutoria-api and tutoria-worker; read-only here.
/// </summary>
public class PostgresChatHistory
{
    private const string Columns =
        "\"ConversationId\", \"Ts\", \"MessageId\", \"StudentId\", \"ModuleId\", \"Question\", \"Response\", " +
        "\"ModelUsed\", \"Provider\", \"HasFile\", \"FileName\", \"TokenCount\", \"ResponseTime\", \"CreatedAt\"";

    /// <summary>Which column a query filters on (the DynamoDB key or GSI it replaces).</summary>
    public enum By { Conversation, Module, Student, Provider }

    private readonly TutoriaDbContext _db;

    public PostgresChatHistory(TutoriaDbContext db) => _db = db;

    /// <summary>The SQL for a query; public for the tests.</summary>
    public static string Sql(By? by, bool hasStart, bool hasEnd)
    {
        var where = new List<string> { "\"ExpiresAt\" > now()" };
        if (by is not null)
        {
            where.Add(by switch
            {
                By.Conversation => "\"ConversationId\" = @key",
                By.Module => "\"ModuleId\" = @key",
                By.Student => "\"StudentId\" = @key",
                _ => "\"Provider\" = @key",
            });
        }
        if (hasStart) where.Add("\"Ts\" >= @start");
        if (hasEnd) where.Add("\"Ts\" <= @end");
        return $"SELECT {Columns} FROM \"ChatHistory\" WHERE {string.Join(" AND ", where)} " +
               "ORDER BY \"Ts\" DESC, \"Id\" DESC LIMIT @limit";
    }

    public async Task<List<ChatMessageDto>> QueryAsync(By? by, object? key, DateTime? start, DateTime? end, int limit)
    {
        var connection = await PostgresStores.OpenConnectionAsync(_db);
        await using var command = connection.CreateCommand();
        command.CommandText = Sql(by, start.HasValue, end.HasValue);
        if (by is not null) AddParameter(command, "key", key!);
        if (start.HasValue) AddParameter(command, "start", new DateTimeOffset(start.Value).ToUnixTimeMilliseconds());
        if (end.HasValue) AddParameter(command, "end", new DateTimeOffset(end.Value).ToUnixTimeMilliseconds());
        AddParameter(command, "limit", limit);

        var items = new List<ChatMessageDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ChatMessageDto
            {
                ConversationId = reader.GetString(0),
                Timestamp = reader.GetInt64(1),
                MessageId = reader.GetString(2),
                StudentId = reader.GetInt32(3),
                ModuleId = reader.GetInt32(4),
                Question = reader.GetString(5),
                Response = reader.GetString(6),
                ModelUsed = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Provider = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                HasFile = reader.GetBoolean(9),
                FileName = reader.IsDBNull(10) ? null : reader.GetString(10),
                TokenCount = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                ResponseTime = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                // ISO like the DynamoDB attribute (UTC, no offset).
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(13).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
            });
        }
        return items;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
