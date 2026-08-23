using System.Diagnostics;
using System.Globalization;

namespace HattrickAI.CHPP;

public static class ChppTraceHttp
{
    public static async Task<string> GetXmlAsync(
        ChppOAuthClient oauth,
        string file,
        IDictionary<string, string?> query,
        string context,
        CancellationToken cancellationToken = default)
    {
        var totalStarted = Stopwatch.GetTimestamp();
        var cacheStarted = Stopwatch.GetTimestamp();
        try
        {
            var cached = await ChppXmlCache.TryGetAsync(file, query, cancellationToken);
            var cacheLookupMs = (long)Stopwatch.GetElapsedTime(cacheStarted).TotalMilliseconds;
            if (cached is not null)
            {
                Console.WriteLine($"CHPP CACHE HIT: {file} ({context})");
                using var cachedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                ChppRequestTrace.Record(file, context, query, "GET", cachedResponse,
                    (long)Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds,
                    cacheHit: true, cacheLookupMs: cacheLookupMs);
                return cached;
            }

            var networkStarted = Stopwatch.GetTimestamp();
            var body = await oauth.GetXmlAsync(file, query, cancellationToken);
            var networkMs = (long)Stopwatch.GetElapsedTime(networkStarted).TotalMilliseconds;

            var cacheWriteStarted = Stopwatch.GetTimestamp();
            await ChppXmlCache.SetAsync(file, query, body, cancellationToken);
            var cacheWriteMs = (long)Stopwatch.GetElapsedTime(cacheWriteStarted).TotalMilliseconds;

            using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            ChppRequestTrace.Record(file, context, query, "GET", response,
                (long)Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds,
                cacheHit: false, cacheLookupMs: cacheLookupMs,
                networkMs: networkMs, cacheWriteMs: cacheWriteMs);
            return body;
        }
        catch (Exception ex)
        {
            var status = ExtractStatusCode(ex.Message);
            using var response = status.HasValue ? new HttpResponseMessage((System.Net.HttpStatusCode)status.Value) : null;
            ChppRequestTrace.Record(file, context, query, "GET", response,
                (long)Stopwatch.GetElapsedTime(totalStarted).TotalMilliseconds,
                ex, cacheHit: false);
            throw;
        }
    }

    private static int? ExtractStatusCode(string message)
    {
        var marker = "CHPP isteği başarısız (";
        var start = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = message.IndexOf(')', start);
        if (end < 0) return null;
        return int.TryParse(message[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var status) ? status : null;
    }
}
