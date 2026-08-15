namespace HattrickAI.CHPP;

public sealed record ChppCredentials(string ConsumerKey, string ConsumerSecret);

public static class ChppSettings
{
    public const string RequestedScopes = "set_matchorder,manage_youthplayers";

    public static ChppCredentials Load(IConfiguration configuration)
    {
        var key = configuration["CHPP_CONSUMER_KEY"];
        var secret = configuration["CHPP_CONSUMER_SECRET"];

        Console.WriteLine($"CHPP config: consumer key configured={!string.IsNullOrWhiteSpace(key)}, consumer secret configured={!string.IsNullOrWhiteSpace(secret)}");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("CHPP_CONSUMER_KEY ve CHPP_CONSUMER_SECRET Render Environment Variables içinde tanımlanmalı.");

        return new ChppCredentials(key.Trim(), secret.Trim());
    }
}
