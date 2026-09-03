using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// FAZ I: Long Shots opportunity layer regression.
/// The paper publishes the LMR -> long-shot conversion range (6%-43%).
/// Scoring a long shot remains a separate M9 calibration gap because the paper
/// presents that relationship graphically rather than as a published equation.
/// </summary>
public static class LongShotOpportunityRegression
{
    public static int Run()
    {
        const double regularVolume = M8ChanceAllocationEngine.PaperExpectedRegularSectorChances;

        var normal = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.Normal, 0);
        var low = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.LongShots, 0);
        var high = M8ChanceAllocationEngine.Calculate(10, 10, AdvancedTactic.LongShots, 10);

        Check(Math.Abs(low.LongShotConversionRate - M8ChanceAllocationEngine.LongShotsMinConversion) < 1e-12,
            "LS level 0 conversion = 6%", out var failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(high.LongShotConversionRate - M8ChanceAllocationEngine.LongShotsMaxConversion) < 1e-12,
            "LS level 10 conversion = 43%", out failure);
        if (failure is not null) return Fail(failure);

        Check(high.LongShotConversionRate > low.LongShotConversionRate,
            "LS conversion increases with tactic strength", out failure);
        if (failure is not null) return Fail(failure);

        var lowExpected = regularVolume * low.LongShotConversionRate;
        var highExpected = regularVolume * high.LongShotConversionRate;
        Check(Math.Abs(lowExpected - regularVolume * 0.06) < 1e-12,
            "LS low-strength opportunity volume = LMR x 6%", out failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(highExpected - regularVolume * 0.43) < 1e-12,
            "LS high-strength opportunity volume = LMR x 43%", out failure);
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
