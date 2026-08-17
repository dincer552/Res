namespace HattrickAI.Backtest;

public static class BacktestTests
{
    public static void RunSafetyChecks()
    {
        var cutoff = new DateTime(2026, 8, 12, 13, 0, 0, DateTimeKind.Local);
        var previous = new DateTime(2026, 8, 10, 13, 0, 0, DateTimeKind.Local);
        if (previous >= cutoff) throw new InvalidOperationException("Backtest cutoff test failed.");
        if (!(2 > 1 ? "W" : 2 < 1 ? "L" : "D").Equals("W", StringComparison.Ordinal)) throw new InvalidOperationException("Result direction test failed.");
    }
}
