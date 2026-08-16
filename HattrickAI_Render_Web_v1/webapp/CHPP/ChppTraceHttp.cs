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
        var started = Stopwatch.GetTimestamp();
        try
        {
            var cached = await ChppXmlCache.TryGetAsync(file, query, cancellationToken);
            if (cached is not null)
            {
                Console.WriteLine($"CHPP CACHE HIT: {file} ({context})");
                return cached;
            }

            var body = await oauth.GetXmlAsync(file, query, cancellationToken);
            await ChppXmlCache.SetAsync(file, query, body, cancellationToken);
            using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            ChppRequestTrace.Record(file, context, query, "GET", response,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                responseBodyPreview: body);
            return body;
        }
        catch (Exception ex)
        {
            var status = ExtractStatusCode(ex.Message);
            using var response = status.HasValue ? new HttpResponseMessage((System.Net.HttpStatusCode)status.Value) : null;
            ChppRequestTrace.Record(file, context, query, "GET", response,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                ex, ex.Message);
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
