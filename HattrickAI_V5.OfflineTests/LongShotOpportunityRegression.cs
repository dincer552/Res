using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// FAZ I: Long Shots opportunity layer regression.
/// V5 tactic strength is mapped to paper RT before applying the exact
/// Constantinou et al. (2026) equation. The published 6%-43% values are
/// empirical ranges; the exact equation is the production mechanism here.
/// </summary>
public static class LongShotOpportunityRegression
{
    public static int Run()
    {
        const double regularVolume = M8ChanceAllocationEngine.PaperExpectedRegularSectorChances;
        const double expectedLow = 0.07520052; // paper RT=0
        const double expectedHigh = 0.22758752; // paper RT=20

        var normal = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.Normal, 0);
        var low = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.LongShots, 0);
        var high = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.LongShots, 10);

        Check(Math.Abs(low.LongShotConversionRate - expectedLow) < 1e-12,
            "LS level 0 follows exact paper equation at RT=0", out var failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(high.LongShotConversionRate - expectedHigh) < 1e-12,
            "LS level 10 follows exact paper equation at RT=20", out failure);
        if (failure is not null) return Fail(failure);

        Check(high.LongShotConversionRate > low.LongShotConversionRate,
            "LS conversion increases with tactic strength", out failure);
        if (failure is not null) return Fail(failure);

        var lowExpected = regularVolume * low.LongShotConversionRate;
        var highExpected = regularVolume * high.LongShotConversionRate;
        Check(Math.Abs(lowExpected - regularVolume * expectedLow) < 1e-12,
            "LS low-strength opportunity volume = LMR x exact TCR", out failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(highExpected - regularVolume * expectedHigh) < 1e-12,
            "LS high-strength opportunity volume = LMR x exact TCR", out failure);
        if (failure is not null) return Fail(failure);

        Check(highExpected <= regularVolume + 1e-12,
            "LS opportunity cannot exceed LMR volume", out failure);
        if (failure is not null) return Fail(failure);

        Check(normal.LongShotConversionRate == 0.0,
            "Normal tactic has no tactical LS conversion", out failure);
        if (failure is not null) return Fail(failure);

        Console.WriteLine($"PASS: FAZ I Long Shots opportunity | LMR={regularVolume:0.###} | LS={lowExpected:0.###}->{highExpected:0.###}");
        return 0;
    }

    private static void Check(bool condition, string message, out string? failure)
        => failure = condition ? null : message;

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }
}
