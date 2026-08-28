namespace HattrickAI.CHPP;

public sealed record ChppCredentials(string ConsumerKey, string ConsumerSecret);

public static class ChppSettings
{
    // CHPP Consumer Key is intentionally kept in source as requested.
    // The Consumer Secret remains an environment variable and is never stored here.
    private const string EmbeddedConsumerKey = "4CzYYAnSg7SSHkQyDVMLIV";

    public const string RequestedScopes = "set_matchorder,manage_youthplayers";

    public static ChppCredentials Load(IConfiguration configuration)
    {
        var configuredKey = configuration["CHPP_CONSUMER_KEY"];
        var key = string.IsNullOrWhiteSpace(configuredKey) ? EmbeddedConsumerKey : configuredKey.Trim();
        var secret = configuration["CHPP_CONSUMER_SECRET"];

        Console.WriteLine($"CHPP config: consumer key configured={(key.Length > 0)}, consumer secret configured={!string.IsNullOrWhiteSpace(secret)}");

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("CHPP_CONSUMER_SECRET Render Environment Variables içinde tanımlanmalı.");

        return new ChppCredentials(key, secret.Trim());
    }
}
