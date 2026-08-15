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
            var body = await oauth.GetXmlAsync(file, query, cancellationToken);
            ChppRequestTrace.Record(
                file,
                context,
                query,
                "GET",
                new HttpResponseMessage(System.Net.HttpStatusCode.OK),
                Stopwatch.GetElapsedTime(started).Milliseconds,
                responseBodyPreview: body);
            return body;
        }
        catch (Exception ex)
        {
            var status = ExtractStatusCode(ex.Message);
            using var response = status.HasValue ? new HttpResponseMessage((System.Net.HttpStatusCode)status.Value) : null;
            ChppRequestTrace.Record(
                file,
                context,
                query,
                "GET",
                response,
                Stopwatch.GetElapsedTime(started).Milliseconds,
                ex,
                ex.Message);
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
        return int.TryParse(message[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var status)
            ? status
            : null;
    }
}
