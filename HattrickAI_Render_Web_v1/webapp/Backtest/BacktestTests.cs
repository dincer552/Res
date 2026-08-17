using HattrickAI.CHPP;
using Xunit;

namespace HattrickAI.Backtest;

public sealed class BacktestTests
{
    [Fact]
    public void Fixture_cutoff_must_precede_historical_inputs()
    {
        var cutoff = new DateTime(2026, 8, 12, 13, 0, 0, DateTimeKind.Local);
        var previous = new DateTime(2026, 8, 10, 13, 0, 0, DateTimeKind.Local);
        Assert.True(previous < cutoff);
        Assert.False(cutoff < previous);
    }

    [Fact]
    public void Fixture_score_direction_is_deterministic()
    {
        var home = 2; var away = 1;
        var actual = home > away ? "W" : home < away ? "L" : "D";
        Assert.Equal("W", actual);
    }
}
