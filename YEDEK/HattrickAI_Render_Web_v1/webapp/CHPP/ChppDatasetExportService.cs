using System.Text.Json;
using Npgsql;
using HattrickAI.CHPP;

namespace HattrickAI.Web.CHPP;

/// <summary>
/// Creates a portable, token/session-free dataset for offline HattrickAI testing.
/// </summary>
public sealed class ChppDatasetExportService
{
    private static readonly string[] SensitiveFragments = ["token", "secret", "password", "credential", "oauth", "session", "cookie", "authorization", "refresh"];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<byte[]> BuildAsync(ChppOAuthClient oauth, IConfiguration configuration, CancellationToken ct = default)
    {
        var team = await new ChppTeamDataService(oauth).LoadOwnTeamAsync();
        object? training = null;
        try { training = await new ChppTrainingDataService(oauth).LoadOwnTrainingAsync(ct); } catch { }
        IReadOnlyList<ChppFixture> fixtures = Array.Empty<ChppFixture>();
        try { fixtures = await new ChppMatchDataService(oauth).LoadUpcomingFixturesAsync(team.TeamId); } catch { }
        var postgres = await ExportPostgresAsync(GetConnectionString(configuration), ct);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            exportedAtUtc = DateTime.UtcNow,
            purpose = "HattrickAI offline lineup/simulation testing",
            security = new { credentialsIncluded = false, oauthTokensIncluded = false, sessionCookiesIncluded = false, sensitiveColumnsExcluded = true },
            chpp = new { team, training, fixtures },
            postgres
        }, _json);
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("Default") ?? Environment.GetEnvironmentVariable("DATABASE_URL") ?? Environment.GetEnvironmentVariable("POSTGRES_URL") ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "postgres" && uri.Scheme != "postgresql")) return value;
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]),
            Password = uri.UserInfo.Contains(':') ? Uri.UnescapeDataString(uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]) : null,
            SslMode = SslMode.Prefer
        };
        return b.ConnectionString;
    }

    private static async Task<object> ExportPostgresAsync(string connectionString, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new { configured = false, tables = Array.Empty<object>() };
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var tableNames = new List<(string Schema, string Name)>();
        const string tableSql = "select table_schema, table_name from information_schema.tables where table_type = 'BASE TABLE' and table_schema not in ('pg_catalog','information_schema') order by table_schema, table_name";
        await using (var command = new NpgsqlCommand(tableSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var schema = reader.GetString(0); var name = reader.GetString(1);
                if (!IsSensitiveName(schema) && !IsSensitiveName(name)) tableNames.Add((schema, name));
            }
        }
        var tables = new List<object>();
        foreach (var (schema, name) in tableNames)
        {
            var columns = await ReadSafeColumnsAsync(connection, schema, name, ct);
            if (columns.Count == 0) continue;
            var quoted = string.Join(", ", columns.Select(QuoteIdentifier));
            await using var command = new NpgsqlCommand($"select {quoted} from {QuoteIdentifier(schema)}.{QuoteIdentifier(name)} limit 50000", connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            tables.Add(new { schema, name, columns, rowCount = rows.Count, rows });
        }
        return new { configured = true, tables };
    }

    private static async Task<List<string>> ReadSafeColumnsAsync(NpgsqlConnection connection, string schema, string table, CancellationToken ct)
    {
        const string sql = "select column_name from information_schema.columns where table_schema = $1 and table_name = $2 order by ordinal_position";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(schema); command.Parameters.AddWithValue(table);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<string>();
        while (await reader.ReadAsync(ct)) { var name = reader.GetString(0); if (!IsSensitiveName(name)) result.Add(name); }
        return result;
    }

    private static bool IsSensitiveName(string value) => SensitiveFragments.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));
    private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
