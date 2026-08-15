using System.Text.Json;

namespace HattrickAI.CHPP;

public sealed record ChppCallTraceEntry(
    int Sequence, string File, string Context, string Query, string Method,
    int? HttpStatus, string? Reason, long DurationMs, bool Success,
    string? ErrorType, string? Error, string? ResponseBodyPreview);

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
        string? responseBodyPreview = null)
    {
        var trace = CurrentSlot.Value;
        if (trace == null) return;
        var safeQuery = query == null ? string.Empty : string.Join("&", query.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Key}={x.Value}"));
        var entry = new ChppCallTraceEntry(++trace._sequence, file,
            string.IsNullOrWhiteSpace(context) ? "fixture-view" : context, safeQuery, method,
            response == null ? null : (int)response.StatusCode, response?.ReasonPhrase, durationMs,
            exception == null && response?.IsSuccessStatusCode == true, exception?.GetType().Name,
            exception?.Message, RedactAndLimit(responseBodyPreview));
        trace._calls.Add(entry);
        Console.WriteLine($"CHPP_TRACE {JsonSerializer.Serialize(entry)}");
    }

    public object ToResponse() => new
    {
        operation = Operation, matchId = MatchId, recentIndex = RecentIndex,
        startedAt = StartedAt, callCount = Calls.Count, calls = Calls
    };

    private static string? RedactAndLimit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > 1600 ? value[..1600] + "..." : value;
    }

    private sealed class Scope : IDisposable
    {
        private readonly ChppRequestTrace? _previous;
        private bool _disposed;
        public Scope(ChppRequestTrace? previous) => _previous = previous;
        public void Dispose()
        { if (_disposed) return; _disposed = true; CurrentSlot.Value = _previous; }
    }
}
