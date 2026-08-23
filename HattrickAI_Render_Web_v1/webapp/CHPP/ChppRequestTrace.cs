using System.Text.Json;

namespace HattrickAI.CHPP;

public sealed record ChppCallTraceEntry(
    int Sequence, string File, string Context, string Query, string Method,
    int? HttpStatus, string? Reason, long DurationMs, bool Success,
    bool CacheHit, long? CacheLookupMs, long? NetworkMs, long? CacheWriteMs,
    string? ErrorType, string? Error);

public sealed class ChppRequestTrace
{
    private static readonly AsyncLocal<ChppRequestTrace?> CurrentSlot = new();
    private readonly List<ChppCallTraceEntry> _calls = new();
    private int _sequence;
    public string Operation { get; }
    public int MatchId { get; }
    public int? RecentIndex { get; }
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public static ChppRequestTrace? Current => CurrentSlot.Value;
    public IReadOnlyList<ChppCallTraceEntry> Calls => _calls;

    private ChppRequestTrace(string operation, int matchId, int? recentIndex)
    { Operation = operation; MatchId = matchId; RecentIndex = recentIndex; }

    public static IDisposable Begin(string operation, int matchId, int? recentIndex)
    {
        var previous = CurrentSlot.Value;
        CurrentSlot.Value = new ChppRequestTrace(operation, matchId, recentIndex);
        return new Scope(previous);
    }

    public static void Record(string file, string context, IEnumerable<KeyValuePair<string, string?>>? query,
        string method, HttpResponseMessage? response, long durationMs, Exception? exception = null,
        bool cacheHit = false, long? cacheLookupMs = null, long? networkMs = null, long? cacheWriteMs = null)
    {
        var trace = CurrentSlot.Value;
        if (trace == null) return;
        var safeQuery = query == null ? string.Empty : string.Join("&", query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Key}={(IsSensitiveKey(x.Key) ? "***" : x.Value)}"));
        var entry = new ChppCallTraceEntry(++trace._sequence, file,
            string.IsNullOrWhiteSpace(context) ? "fixture-view" : context, safeQuery, method,
            response == null ? null : (int)response.StatusCode, response?.ReasonPhrase, durationMs,
            exception == null && response?.IsSuccessStatusCode == true, cacheHit,
            cacheLookupMs, networkMs, cacheWriteMs, exception?.GetType().Name, exception?.Message);
        trace._calls.Add(entry);
        Console.WriteLine($"CHPP_TRACE {JsonSerializer.Serialize(entry)}");
    }

    public object ToResponse() => new
    {
        operation = Operation, matchId = MatchId, recentIndex = RecentIndex,
        startedAt = StartedAt, callCount = Calls.Count, calls = Calls
    };

    private static bool IsSensitiveKey(string key)
        => key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("authorization", StringComparison.OrdinalIgnoreCase);

    private sealed class Scope : IDisposable
    {
        private readonly ChppRequestTrace? _previous;
        private bool _disposed;
        public Scope(ChppRequestTrace? previous) => _previous = previous;
        public void Dispose()
        { if (_disposed) return; _disposed = true; CurrentSlot.Value = _previous; }
    }
}
