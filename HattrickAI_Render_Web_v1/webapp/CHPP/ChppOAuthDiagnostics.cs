namespace HattrickAI.CHPP;

public static class ChppOAuthDiagnostics
{
    public static string LogPath => "";
    public static Task LogRequestAsync(string stage, string method, string url, IReadOnlyDictionary<string,string> oauthParameters, string signatureBaseString, string signature, string consumerKey, string consumerSecret) => Task.CompletedTask;
    public static Task LogResponseAsync(string stage, HttpResponseMessage response, string body, string consumerSecret) => Task.CompletedTask;
    public static Task LogFallbackAsync(string stage, string url, string signatureBaseString) => Task.CompletedTask;
    public static Task ClearAsync() => Task.CompletedTask;
}
