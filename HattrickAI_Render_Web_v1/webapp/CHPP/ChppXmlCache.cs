using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HattrickAI.CHPP;

internal static class ChppXmlCache
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string DbPath = ResolvePath();
    private static int _initialized;

    public static async Task<string?> TryGetAsync(
        string file,
        IDictionary<string, string?> query,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var ttl = GetTtl(file);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload, cached_at_utc FROM chpp_xml_cache WHERE cache_key = $key";
            command.Parameters.AddWithValue("$key", BuildKey(file, query));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            var payload = reader.GetString(0);
            var cachedAt = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (ttl.HasValue && DateTime.UtcNow - cachedAt > ttl.Value) return null;
            return payload;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CHPP CACHE READ ERROR: {ex.Message}");
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task SetAsync(
        string file,
        IDictionary<string, string?> query,
        string payload,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO chpp_xml_cache(cache_key, file, payload, cached_at_utc)
VALUES($key, $file, $payload, $cachedAt)
ON CONFLICT(cache_key) DO UPDATE SET
    file = excluded.file,
    payload = excluded.payload,
    cached_at_utc = excluded.cached_at_utc;";
            command.Parameters.AddWithValue("$key", BuildKey(file, query));
            command.Parameters.AddWithValue("$file", file);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$cachedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CHPP CACHE WRITE ERROR: {ex.Message}");
        }
        finally
        {
            Gate.Release();
        }
    }

    private static TimeSpan? GetTtl(string file) => file.ToLowerInvariant() switch
    {
        "matchdetails" => null,
        "matchlineup" => null,
        "matches" => TimeSpan.FromMinutes(5),
        "teamdetails" => TimeSpan.FromMinutes(5),
        "players" => TimeSpan.FromMinutes(5),
        "playerdetails" => TimeSpan.FromHours(24),
        _ => TimeSpan.FromMinutes(2)
    };

    private static string BuildKey(string file, IDictionary<string, string?> query)
    {
        var canonical = file + "|" + string.Join("&", query
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value ?? ""}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={DbPath};Cache=Shared");
        return connection;
    }

    private static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
        try
        {
            var directory = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            using var connection = OpenConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS chpp_xml_cache (
    cache_key TEXT PRIMARY KEY,
    file TEXT NOT NULL,
    payload TEXT NOT NULL,
    cached_at_utc TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _initialized, 0);
            Console.Error.WriteLine($"CHPP CACHE INIT ERROR: {ex.Message}");
        }
    }

    private static string ResolvePath()
    {
        var configured = Environment.GetEnvironmentVariable("HATTRICKAI_CHPP_CACHE_DB");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var renderData = Environment.GetEnvironmentVariable("HATTRICKAI_CACHE_DB_PATH");
        if (!string.IsNullOrWhiteSpace(renderData)) return renderData;
        return Path.Combine(Path.GetTempPath(), "hattrickai-chpp-cache.db");
    }
}
