using System.Text.RegularExpressions;

namespace HattrickAI.CHPP;

public static class ChppOAuthDiagnostics
{
    public static string LogPath => Path.Combine(Path.GetTempPath(), "hattrickai-chpp-oauth.log");

    public static Task LogRequestAsync(
        string stage,
        string method,
        string url,
        IReadOnlyDictionary<string, string> oauthParameters,
        string signatureBaseString,
        string signature,
        string consumerKey,
        string consumerSecret)
    {
        var safeKey = Mask(consumerKey);
        var safeUrl = RedactSecrets(url);
        var parameters = string.Join(", ", oauthParameters.Keys.OrderBy(x => x));
        return WriteAsync($"REQUEST | {stage} | {method} | {safeUrl} | key={safeKey} | oauthParams=[{parameters}]");
    }

    public static Task LogResponseAsync(
        string stage,
        HttpResponseMessage response,
        string body,
        string consumerSecret)
    {
        var safeBody = RedactSecrets(body);
        if (safeBody.Length > 1200)
            safeBody = safeBody[..1200] + "...";

        return WriteAsync(
            $"RESPONSE | {stage} | {(int)response.StatusCode} {response.ReasonPhrase} | body={safeBody}");
    }

    public static Task LogFallbackAsync(string stage, string url, string signatureBaseString) =>
        WriteAsync($"FALLBACK | {stage} | {RedactSecrets(url)}");

    public static Task ClearAsync()
    {
        try
        {
            if (File.Exists(LogPath))
                File.Delete(LogPath);
        }
        catch
        {
            // Diagnostics must never break OAuth.
        }

        return Task.CompletedTask;
    }

    private static async Task WriteAsync(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} | {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(LogPath, line);
            Console.Write(line);
        }
        catch
        {
            // Diagnostics must never break OAuth.
        }
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<missing>";

        return value.Length <= 6
            ? "***"
            : value[..3] + "***" + value[^3..];
    }

    private static string RedactSecrets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = value;
        result = Regex.Replace(result, @"(oauth_token_secret=)[^&\s]+", "$1<redacted>", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"(oauth_consumer_secret=)[^&\s]+", "$1<redacted>", RegexOptions.IgnoreCase);
        return result;
    }
}
