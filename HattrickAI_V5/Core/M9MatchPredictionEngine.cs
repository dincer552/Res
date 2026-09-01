using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: converts the calibrated/structural M8 chance result into a bounded
/// match prediction contract. The conversion is deliberately isolated so the
/// coefficients can be replaced by historical-match calibration later.
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

        // M8 owns chance structure; M9 only translates it into a bounded
        // expectation and outcome probabilities. No XI/behaviour re-selection.
        var ownExpected = ClampGoals(BaseGoals + ChanceScale * ownChance);
        var opponentExpected = ClampGoals(BaseGoals + ChanceScale * (1.0 - ownChance));

        // Small bounded matchup correction keeps M9 responsive to the complete
        // M7/M8 matchup while preventing a single raw score from dominating.
        var correction = 0.20 * matchup;
        ownExpected = ClampGoals(ownExpected + correction);
        opponentExpected = ClampGoals(opponentExpected - correction);

        // Venue is an explicit M7 input. Keep the M9 adjustment tiny because
        // the rating engine already owns the core venue effect.
        if (location == MatchLocation.Home)
            ownExpected = ClampGoals(ownExpected + 0.08);
        else if (location == MatchLocation.Away)
            opponentExpected = ClampGoals(opponentExpected + 0.04);

        var possession = midfieldShare;
        var spread = ownExpected - opponentExpected;
        var win = Logistic(2.20 * spread);
        var draw = 0.18 + 0.20 * Math.Exp(-Math.Abs(spread) * 1.6);
        draw = Math.Clamp(draw, 0.08, 0.32);

        // Normalize the three mutually-exclusive outcomes.
        var remaining = Math.Max(0.001, win + draw + (1.0 - win));
        draw = draw / remaining;
        var nonDraw = 1.0 - draw;
        win = nonDraw * win;
        var loss = nonDraw * (1.0 - win / Math.Max(nonDraw, 0.001));
        loss = Math.Clamp(loss, 0.0, 1.0);
        var total = win + draw + loss;
        win /= total;
        draw /= total;
        loss /= total;

        var prediction = new MatchPrediction(
            possession,
            ownExpected,
            opponentExpected,
            win,
            draw,
            loss);

        return new M9PredictionResult(
            candidate.Lineup.Formation,
            CandidateId(candidate.Lineup),
            prediction,
            ownChance,
            M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration);
    }

    private static double Logistic(double value)
        => 1.0 / (1.0 + Math.Exp(-Math.Clamp(value, -12.0, 12.0)));

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
