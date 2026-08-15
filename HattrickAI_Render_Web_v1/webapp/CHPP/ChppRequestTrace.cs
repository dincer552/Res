using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HattrickAI.CHPP;

public sealed record ChppCallTraceEntry(
    int Sequence,
    string File,
    string Context,
    string Query,
    string Method,
    int? HttpStatus,
    string? Reason,
    long DurationMs,
    bool Success,
    string? ErrorType,
    string? Error,
    string? ResponseBodyPreview);

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
    {
        Operation = operation;
        MatchId = matchId;
        RecentIndex = recentIndex;
    }

    public static IDisposable Begin(string operation, int matchId, int? recentIndex)
    {
        var previous = CurrentSlot.Value;
        CurrentSlot.Value = new ChppRequestTrace(operation, matchId, recentIndex);
        return new Scope(previous);
    }

    public static void Record(
        string file,
        string context,
        IReadOnlyDictionary<string, string?>? query,
        string method,
        HttpResponseMessage? response,
        long durationMs,
        Exception? exception = null,
        string? responseBodyPreview = null)
    {
        var trace = CurrentSlot.Value;
        if (trace == null)
            return;

        var safeQuery = query == null
            ? string.Empty
            : string.Join("&", query.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Key}={x.Value}"));

        var success = exception == null && response?.IsSuccessStatusCode == true;
        var body = RedactAndLimit(responseBodyPreview);
        var entry = new ChppCallTraceEntry(
            ++trace._sequence,
            file,
            string.IsNullOrWhiteSpace(context) ? "fixture-view" : context,
            safeQuery,
            method,
            response == null ? null : (int)response.StatusCode,
            response?.ReasonPhrase,
            durationMs,
            success,
            exception?.GetType().Name,
            exception?.Message,
            body);

        trace._calls.Add(entry);
        Console.WriteLine($"CHPP_TRACE {JsonSerializer.Serialize(entry)}");
    }

    public object ToResponse()
    {
        return new
        {
            operation = Operation,
            matchId = MatchId,
            recentIndex = RecentIndex,
            startedAt = StartedAt,
            callCount = Calls.Count,
            calls = Calls
        };
    }

    public void RecordException(string stage, Exception exception)
    {
        Console.WriteLine($"CHPP_TRACE_PIPELINE_ERROR stage={stage} type={exception.GetType().Name} error={exception.Message}");
    }

    private static string? RedactAndLimit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var result = value
            .Replace("oauth_token_secret", "oauth_token_secret", StringComparison.OrdinalIgnoreCase);
        if (result.Length > 1600)
            result = result[..1600] + "...";
        return result;
    }

    private sealed class Scope : IDisposable
    {
        private readonly ChppRequestTrace? _previous;
        private bool _disposed;

        public Scope(ChppRequestTrace? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentSlot.Value = _previous;
        }
    }
}
