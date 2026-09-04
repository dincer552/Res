using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Historical calibration layer for direct set-piece conversion.
/// Hattrick exposes the taker's Set Pieces skill and the keeper's Goalkeeping/
/// Set Pieces skills to the match engine, but the published 2026 paper does not
/// publish the hidden taker-to-goal conversion equation. Therefore this class
/// estimates the conversion empirically when match-level taker observations are
/// supplied; it never invents coefficients from the current fixture.
/// </summary>
public sealed record SetPieceTakerObservation(
    string MatchId,
    int HatStatsHome,
    int HatStatsAway,
    int TakerSetPiecesSkill,
    int KeeperGoalkeepingSkill,
    int KeeperSetPiecesSkill,
    string SetPieceType,
    int Attempts,
    int Goals);

public sealed record SetPieceTakerBin(
    int TakerSetPiecesSkill,
    int Attempts,
    int Goals,
    double SmoothedConversion,
    double RawConversion,
    int Matches);

public sealed record SetPieceTakerCalibrationReport(
    string Source,
    int InputMatches,
    int EligibleMatches,
    int TotalAttempts,
    int TotalGoals,
    double RawConversion,
    double SmoothedConversion,
    double MeanAbsoluteError,
    IReadOnlyList<SetPieceTakerBin> ByTakerSkill,
    bool ProductionEligible,
    string ProductionDecision);

public static class SetPieceTakerCalibrationEngine
{
    public const int MinimumHatStats = 333;
    public const int MinimumMatchesForProduction = 250;
    public const int MinimumAttemptsForProduction = 250;
    public const double BetaPriorAlpha = 1.0;
    public const double BetaPriorBeta = 1.0;

    public static SetPieceTakerCalibrationReport Analyze(
        IEnumerable<SetPieceTakerObservation> observations,
        int minimumHatStats = MinimumHatStats,
        int minimumMatchesForProduction = MinimumMatchesForProduction,
        int minimumAttemptsForProduction = MinimumAttemptsForProduction)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var input = observations
            .Where(x => !string.IsNullOrWhiteSpace(x.MatchId))
            .Select(Normalize)
            .ToArray();

        var eligible = input
            .Where(x => x.HatStatsHome >= minimumHatStats && x.HatStatsAway >= minimumHatStats && x.Attempts > 0)
            .ToArray();

        var attempts = eligible.Sum(x => x.Attempts);
        var goals = eligible.Sum(x => x.Goals);
        var raw = attempts == 0 ? 0.0 : (double)goals / attempts;
        var smoothed = (goals + BetaPriorAlpha) / (attempts + BetaPriorAlpha + BetaPriorBeta);

        var bins = eligible
            .GroupBy(x => x.TakerSetPiecesSkill)
            .OrderBy(x => x.Key)
            .Select(g =>
            {
                var a = g.Sum(x => x.Attempts);
                var scored = g.Sum(x => x.Goals);
                return new SetPieceTakerBin(
                    g.Key,
                    a,
                    scored,
                    (scored + BetaPriorAlpha) / (a + BetaPriorAlpha + BetaPriorBeta),
                    a == 0 ? 0.0 : (double)scored / a,
                    g.Select(x => x.MatchId).Distinct(StringComparer.Ordinal).Count());
            })
            .ToArray();

        // Cross-validation-style diagnostic: compare each observation's empirical
        // outcome rate with its own skill-bin rate. This is deliberately a
        // calibration diagnostic, not a hidden game-engine equation.
        var bySkill = bins.ToDictionary(x => x.TakerSetPiecesSkill);
        var absoluteError = 0.0;
        var errorWeight = 0;
        foreach (var observation in eligible)
        {
            var bin = bySkill[observation.TakerSetPiecesSkill];
            var observed = observation.Goals / (double)observation.Attempts;
            absoluteError += Math.Abs(observed - bin.SmoothedConversion) * observation.Attempts;
            errorWeight += observation.Attempts;
        }

        var mae = errorWeight == 0 ? 0.0 : absoluteError / errorWeight;
        var productionEligible = eligible.Length >= minimumMatchesForProduction && attempts >= minimumAttemptsForProduction;

        return new SetPieceTakerCalibrationReport(
            "real-CHPP-direct-set-piece-corpus",
            input.Length,
            eligible.Length,
            attempts,
            goals,
            raw,
            smoothed,
            mae,
            bins,
            productionEligible,
            productionEligible
                ? "Candidate taker conversion curve is eligible for production review; keeper skill and set-piece type must still be included in the final multivariable comparison."
                : "Validation only; no production taker conversion is activated until the required historical direct-set-piece corpus is supplied.");
    }

    /// <summary>
    /// Returns the empirical conversion for a taker skill using linear
    /// interpolation between observed skill bins. With no historical bins,
    /// returns NaN rather than silently fabricating a coefficient.
    /// </summary>
    public static double InterpolateObservedConversion(SetPieceTakerCalibrationReport report, double takerSetPiecesSkill)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.ByTakerSkill.Count == 0) return double.NaN;
        if (report.ByTakerSkill.Count == 1) return report.ByTakerSkill[0].SmoothedConversion;

        var ordered = report.ByTakerSkill.OrderBy(x => x.TakerSetPiecesSkill).ToArray();
        if (takerSetPiecesSkill <= ordered[0].TakerSetPiecesSkill) return ordered[0].SmoothedConversion;
        if (takerSetPiecesSkill >= ordered[^1].TakerSetPiecesSkill) return ordered[^1].SmoothedConversion;

        for (var i = 1; i < ordered.Length; i++)
        {
            var low = ordered[i - 1];
            var high = ordered[i];
            if (takerSetPiecesSkill > high.TakerSetPiecesSkill) continue;
            var t = (takerSetPiecesSkill - low.TakerSetPiecesSkill) / (high.TakerSetPiecesSkill - low.TakerSetPiecesSkill);
            return low.SmoothedConversion + t * (high.SmoothedConversion - low.SmoothedConversion);
        }

        return ordered[^1].SmoothedConversion;
    }

    private static SetPieceTakerObservation Normalize(SetPieceTakerObservation x)
        => x with
        {
            TakerSetPiecesSkill = Math.Max(0, x.TakerSetPiecesSkill),
            KeeperGoalkeepingSkill = Math.Max(0, x.KeeperGoalkeepingSkill),
            KeeperSetPiecesSkill = Math.Max(0, x.KeeperSetPiecesSkill),
            Attempts = Math.Max(0, x.Attempts),
            Goals = Math.Clamp(x.Goals, 0, Math.Max(0, x.Attempts)),
            SetPieceType = string.IsNullOrWhiteSpace(x.SetPieceType) ? "Unknown" : x.SetPieceType
        };
}
