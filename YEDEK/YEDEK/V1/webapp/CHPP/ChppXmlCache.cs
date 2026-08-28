using Npgsql;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HattrickAI.CHPP;

internal static class ChppXmlCache
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object InitGate = new();
    private static readonly string? ConnectionString = ResolveConnectionString();
    private static int _initialized;

    public static async Task<string?> TryGetAsync(
        string file,
        IDictionary<string, string?> query,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureInitialized()) return null;

        var ttl = GetTtl(file);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload, cached_at_utc FROM chpp_xml_cache WHERE cache_key = @key";
            command.Parameters.AddWithValue("key", BuildKey(file, query));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            var payload = reader.GetString(0);
            var cachedAt = reader.GetFieldValue<DateTime>(1).ToUniversalTime();
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
        if (!EnsureInitialized()) return;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO chpp_xml_cache(cache_key, file, payload, cached_at_utc)
VALUES(@key, @file, @payload, @cachedAt)
ON CONFLICT(cache_key) DO UPDATE SET
    file = EXCLUDED.file,
    payload = EXCLUDED.payload,
    cached_at_utc = EXCLUDED.cached_at_utc;";
            command.Parameters.AddWithValue("key", BuildKey(file, query));
            command.Parameters.AddWithValue("file", file);
            command.Parameters.AddWithValue("payload", payload);
            command.Parameters.AddWithValue("cachedAt", DateTime.UtcNow);
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

    private static NpgsqlConnection OpenConnection()
        => new(ConnectionString!);

    private static bool EnsureInitialized()
    {
        if (ConnectionString is null)
        {
            Console.Error.WriteLine("CHPP CACHE DISABLED: DATABASE_URL is not configured.");
            return false;
        }

        if (Volatile.Read(ref _initialized) != 0) return true;

        lock (InitGate)
        {
            if (_initialized != 0) return true;
            try
            {
                using var connection = OpenConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS chpp_xml_cache (
    cache_key TEXT PRIMARY KEY,
    file TEXT NOT NULL,
    payload TEXT NOT NULL,
    cached_at_utc TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_chpp_xml_cache_cached_at
    ON chpp_xml_cache(cached_at_utc);";
                command.ExecuteNonQuery();
                Volatile.Write(ref _initialized, 1);
                Console.WriteLine("CHPP CACHE: PostgreSQL persistent cache ready.");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CHPP CACHE INIT ERROR: {ex.Message}");
                return false;
            }
        }
    }

    private static string? ResolveConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("HATTRICKAI_DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured)) return null;

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            return configured;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            SslMode = SslMode.Require
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            builder.Username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length == 2) builder.Password = Uri.UnescapeDataString(parts[1]);
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length != 2) continue;
                if (parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<SslMode>(parts[1], true, out var sslMode))
                    builder.SslMode = sslMode;
            }
        }

        return builder.ConnectionString;
    }
}
