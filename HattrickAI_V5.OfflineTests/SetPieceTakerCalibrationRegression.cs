using System;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class SetPieceTakerCalibrationRegression
{
    public static int Run()
    {
        var observations = new[]
        {
            new SetPieceTakerObservation("a", 400, 390, 8, 7, 5, "DFK", 10, 5),
            new SetPieceTakerObservation("b", 410, 395, 8, 8, 6, "PK", 10, 4),
            new SetPieceTakerObservation("c", 420, 401, 12, 7, 5, "DFK", 10, 7),
            new SetPieceTakerObservation("d", 430, 405, 12, 8, 6, "PK", 10, 6),
        };

        var report = SetPieceTakerCalibrationEngine.Analyze(
            observations,
            minimumMatchesForProduction: 4,
            minimumAttemptsForProduction: 40);

        Check(report.InputMatches == 4, "all observations retained", out var failure);
        if (failure is not null) return Fail(failure);
        Check(report.EligibleMatches == 4, "HatStats >= 333 eligibility", out failure);
        if (failure is not null) return Fail(failure);
        Check(report.TotalAttempts == 40 && report.TotalGoals == 22, "attempt/goal totals", out failure);
        if (failure is not null) return Fail(failure);
        Check(report.ByTakerSkill.Count == 2, "taker skill bins", out failure);
        if (failure is not null) return Fail(failure);
        Check(report.ProductionEligible, "activation gate accepts sufficient corpus", out failure);
        if (failure is not null) return Fail(failure);

        var low = SetPieceTakerCalibrationEngine.InterpolateObservedConversion(report, 8);
        var high = SetPieceTakerCalibrationEngine.InterpolateObservedConversion(report, 12);
        Check(high > low, "observed conversion rises between fixture skill bins", out failure);
        if (failure is not null) return Fail(failure);

        var empty = SetPieceTakerCalibrationEngine.Analyze(Array.Empty<SetPieceTakerObservation>());
        Check(!empty.ProductionEligible && empty.ByTakerSkill.Count == 0, "empty corpus never activates", out failure);
        if (failure is not null) return Fail(failure);

        Console.WriteLine($"PASS: Set-piece taker calibration | matches={report.EligibleMatches} | attempts={report.TotalAttempts} | raw={report.RawConversion:P2} | MAE={report.MeanAbsoluteError:P2}");
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
