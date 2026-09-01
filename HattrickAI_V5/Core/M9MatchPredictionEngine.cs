using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: converts the structural M8 chance result into a bounded match
/// prediction. The conversion is isolated so historical calibration can
/// replace the coefficients without changing M7/M8.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double BaseGoals = 0.35;
    private const double ChanceScale = 2.25;
    private const double MaxGoals = 5.0;

    public M9PredictionResult Predict(
        TacticalCandidate candidate,
        M8ChanceResult chance,
        MatchLocation location)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(chance);

        var ownChance = Clamp01(chance.StructuralChanceIndex);
        var midfieldShare = Clamp01(chance.MidfieldShare);
        var matchup = ClampSigned(candidate.Matchup.OverallScore);

        var ownExpected = ClampGoals(BaseGoals + ChanceScale * ownChance);
        var opponentExpected = ClampGoals(BaseGoals + ChanceScale * (1.0 - ownChance));

        // Keep matchup and venue effects small because M7/M8 already own the
        // underlying rating and chance structure.
        var correction = 0.20 * matchup;
        ownExpected = ClampGoals(ownExpected + correction);
        opponentExpected = ClampGoals(opponentExpected - correction);

        if (location == MatchLocation.Home)
            ownExpected = ClampGoals(ownExpected + 0.08);
        else if (location == MatchLocation.Away)
            opponentExpected = ClampGoals(opponentExpected + 0.04);

        var spread = ownExpected - opponentExpected;
        var winLogit = 2.20 * spread;
        var drawLogit = 0.45 - (0.70 * Math.Abs(spread));
        var lossLogit = -winLogit;

        var max = Math.Max(winLogit, Math.Max(drawLogit, lossLogit));
        var winWeight = Math.Exp(winLogit - max);
        var drawWeight = Math.Exp(drawLogit - max);
        var lossWeight = Math.Exp(lossLogit - max);
        var total = Math.Max(1e-9, winWeight + drawWeight + lossWeight);

        var prediction = new MatchPrediction(
            midfieldShare,
            ownExpected,
            opponentExpected,
            winWeight / total,
            drawWeight / total,
            lossWeight / total);

        return new M9PredictionResult(
            candidate.Lineup.Formation,
            CandidateId(candidate.Lineup),
            prediction,
            ownChance,
            M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
    private static double ClampSigned(double value) => Math.Clamp(value, -1.0, 1.0);
    private static double ClampGoals(double value) => Math.Clamp(value, 0.05, MaxGoals);

    private static string CandidateId(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
}

public sealed record M9PredictionResult(
    string Formation,
    string CandidateId,
    MatchPrediction Prediction,
    double StructuralChanceIndex,
    M9CalibrationStatus CalibrationStatus);

public enum M9CalibrationStatus
{
    StructuralModelAwaitingHistoricalCalibration,
    CalibratedAgainstHistoricalMatches
}
