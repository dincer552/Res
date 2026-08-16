using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using Npgsql;
using NpgsqlTypes;

namespace HattrickAI.Web;

/// <summary>
/// Central persistent cache for opponent historical match data and completed HO analysis.
/// PostgreSQL is the source of truth; CHPP is only used on a cache miss or explicit refresh.
/// </summary>
public sealed class PostgresHistoricalCache
{
    private readonly string? _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public PostgresHistoricalCache(IConfiguration configuration)
    {
        _connectionString = NormalizeConnectionString(configuration["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL"));
    }

    public static string AnalysisKey(int currentMatchId, int historicalMatchId, int ownTeamId, bool isHome, IEnumerable<PlayerData> players)
    {
        var material = string.Join('|', players.OrderBy(p => p.PlayerId).Select(p => $"{p.PlayerId}:{p.Form}:{p.Stamina}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant()[..16];
        return $"analysis:{currentMatchId}:{historicalMatchId}:{ownTeamId}:{(isHome ? 1 : 0)}:{hash}";
    }

    public async Task<ChppSelectedMatch?> GetSelectedMatchAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT payload::text FROM historical_match_cache WHERE cache_key = $1", conn);
            cmd.Parameters.AddWithValue(key);
            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            return value is string json ? JsonSerializer.Deserialize<ChppSelectedMatch>(json, _json) : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"POSTGRES CACHE READ MATCH ERROR: {ex.Message}");
            return null;
        }
    }

    public async Task SetSelectedMatchAsync(string key, ChppSelectedMatch value, int teamId, int opponentTeamId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            var json = JsonSerializer.Serialize(value, _json);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO historical_match_cache(cache_key, team_id, opponent_team_id, match_id, payload, cached_at)
VALUES ($1,$2,$3,$4,$5,$6)
ON CONFLICT(cache_key) DO UPDATE SET payload=EXCLUDED.payload, cached_at=EXCLUDED.cached_at,
 team_id=EXCLUDED.team_id, opponent_team_id=EXCLUDED.opponent_team_id, match_id=EXCLUDED.match_id", conn);
            cmd.Parameters.AddWithValue(key);
            cmd.Parameters.AddWithValue(teamId);
            cmd.Parameters.AddWithValue(opponentTeamId);
            cmd.Parameters.AddWithValue(value.Fixture.MatchId);
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
            cmd.Parameters.AddWithValue(DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) { Console.Error.WriteLine($"POSTGRES CACHE WRITE MATCH ERROR: {ex.Message}"); }
    }

    public async Task<IReadOnlyList<ChppLineupPlayer>?> GetLineupAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT payload::text FROM historical_lineup_cache WHERE cache_key = $1", conn);
            cmd.Parameters.AddWithValue(key);
            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            return value is string json ? JsonSerializer.Deserialize<List<ChppLineupPlayer>>(json, _json) : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"POSTGRES CACHE READ LINEUP ERROR: {ex.Message}");
            return null;
        }
    }

    public async Task SetLineupAsync(string key, int matchId, int teamId, IReadOnlyList<ChppLineupPlayer> value, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            var json = JsonSerializer.Serialize(value, _json);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO historical_lineup_cache(cache_key, match_id, team_id, payload, cached_at)
VALUES ($1,$2,$3,$4,$5)
ON CONFLICT(cache_key) DO UPDATE SET payload=EXCLUDED.payload, cached_at=EXCLUDED.cached_at,
 match_id=EXCLUDED.match_id, team_id=EXCLUDED.team_id", conn);
            cmd.Parameters.AddWithValue(key);
            cmd.Parameters.AddWithValue(matchId);
            cmd.Parameters.AddWithValue(teamId);
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
            cmd.Parameters.AddWithValue(DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) { Console.Error.WriteLine($"POSTGRES CACHE WRITE LINEUP ERROR: {ex.Message}"); }
    }

    public async Task<string?> GetAnalysisAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT payload::text FROM historical_analysis_cache WHERE cache_key = $1", conn);
            cmd.Parameters.AddWithValue(key);
            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            return value as string;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"POSTGRES CACHE READ ANALYSIS ERROR: {ex.Message}");
            return null;
        }
    }

    public async Task SetAnalysisAsync(string key, int currentMatchId, int historicalMatchId, int ownTeamId, string json, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;
        try
        {
            await EnsureSchemaAsync(cancellationToken);
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO historical_analysis_cache(cache_key, current_match_id, historical_match_id, own_team_id, payload, cached_at)
VALUES ($1,$2,$3,$4,$5,$6)
ON CONFLICT(cache_key) DO UPDATE SET payload=EXCLUDED.payload, cached_at=EXCLUDED.cached_at", conn);
            cmd.Parameters.AddWithValue(key);
            cmd.Parameters.AddWithValue(currentMatchId);
            cmd.Parameters.AddWithValue(historicalMatchId);
            cmd.Parameters.AddWithValue(ownTeamId);
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json });
            cmd.Parameters.AddWithValue(DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) { Console.Error.WriteLine($"POSTGRES CACHE WRITE ANALYSIS ERROR: {ex.Message}"); }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady) return;
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(@"
CREATE TABLE IF NOT EXISTS historical_match_cache (
  cache_key TEXT PRIMARY KEY,
  team_id INTEGER NOT NULL,
  opponent_team_id INTEGER NOT NULL,
  match_id INTEGER NOT NULL,
  payload JSONB NOT NULL,
  cached_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_historical_match_cache_opponent ON historical_match_cache(opponent_team_id, cached_at DESC);
CREATE TABLE IF NOT EXISTS historical_lineup_cache (
  cache_key TEXT PRIMARY KEY,
  match_id INTEGER NOT NULL,
  team_id INTEGER NOT NULL,
  payload JSONB NOT NULL,
  cached_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_historical_lineup_cache_match ON historical_lineup_cache(match_id, team_id);
CREATE TABLE IF NOT EXISTS historical_analysis_cache (
  cache_key TEXT PRIMARY KEY,
  current_match_id INTEGER NOT NULL,
  historical_match_id INTEGER NOT NULL,
  own_team_id INTEGER NOT NULL,
  payload JSONB NOT NULL,
  cached_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_historical_analysis_cache_match ON historical_analysis_cache(current_match_id, historical_match_id, own_team_id);", conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = new NpgsqlConnection(_connectionString!);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    private static string? NormalizeConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return raw;
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require
        };
        return builder.ConnectionString;
    }
}
