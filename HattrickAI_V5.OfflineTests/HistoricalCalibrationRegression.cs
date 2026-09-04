using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class HistoricalCalibrationRegression
{
    public static int Run()
    {
        var paper = HistoricalCalibrationEngine.PaperEventReference;
        Check(Math.Abs(paper["Winger"].EventRate - 0.2163) < 1e-12, "Paper Winger rate", out var failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(paper["QuickRush"].GoalRate - 0.3670) < 1e-12, "Paper Quick Rush goal rate", out failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(M8ChanceAllocationEngine.PaperExpectedRegularSectorChances - 8.745) < 1e-12,
            "Paper regular-sector baseline = 8.745", out failure);
        if (failure is not null) return Fail(failure);

        var sample = new HistoricalCalibrationSample(
            "regression-001",
            500,
            500,
            15,
            14,
            3,
            4,
            2,
            2,
            3,
            3,
            "LongShots",
            5,
            1,
            1,
            new Dictionary<string, int> { ["Winger"] = 1, ["QuickRush"] = 1 },
            new Dictionary<string, int> { ["Winger"] = 1 });

        var report = HistoricalCalibrationEngine.Analyze(new[] { sample });
        Check(report.EligibleMatches == 1, "Historical sample passes HatStats gate", out failure);
        if (failure is not null) return Fail(failure);
        Check(report.Events["Winger"].TotalEvents == 1, "Historical event counting", out failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(report.Events["Winger"].GoalRate - 1.0) < 1e-12, "Historical event goal-rate counting", out failure);
        if (failure is not null) return Fail(failure);
        Check(report.LongShots.LongShotAttempts == 1, "Historical Long Shot counting", out failure);
        if (failure is not null) return Fail(failure);
        Check(!report.ProductionEligible, "Small historical sample cannot activate production", out failure);
        if (failure is not null) return Fail(failure);

        var ineligible = sample with { HatStatsHome = 200, HatStatsAway = 200 };
        var blocked = HistoricalCalibrationEngine.Analyze(new[] { ineligible });
        Check(blocked.EligibleMatches == 0, "HatStats <333 is excluded", out failure);
        if (failure is not null) return Fail(failure);
        Check(!blocked.ProductionEligible, "Ineligible corpus cannot activate production", out failure);
        if (failure is not null) return Fail(failure);

        Console.WriteLine("PASS: Historical calibration engine | paper references + CHPP fit/gating + Long Shot calibration diagnostics");
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
