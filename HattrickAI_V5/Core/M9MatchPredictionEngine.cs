using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: converts the structural M8 chance result into a bounded match
/// prediction. Historical calibration can replace the coefficients later.
/// Win/draw/loss probabilities are derived from the same expected-goals pair
/// using a Poisson scoreline model, so the probabilities cannot contradict
/// the direction of the expected goals.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double BaseGoals = 0.35;
    private const double ChanceScale = 2.25;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

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

        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);

        var prediction = new MatchPrediction(
            midfieldShare,
            ownExpected,
            opponentExpected,
            probabilities.Win,
            probabilities.Draw,
            probabilities.Loss);

        return new M9PredictionResult(
            candidate.Lineup.Formation,
            CandidateId(candidate.Lineup),
            prediction,
            ownChance,
            M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration);
    }

    internal static (double Win, double Draw, double Loss) CalculatePoissonOutcomeProbabilities(
        double ownExpected,
        double opponentExpected)
    {
        ownExpected = ClampGoals(ownExpected);
        opponentExpected = ClampGoals(opponentExpected);

        var own = PoissonDistribution(ownExpected, PoissonGoalCutoff);
        var opponent = PoissonDistribution(opponentExpected, PoissonGoalCutoff);

        var win = 0.0;
        var draw = 0.0;
        var loss = 0.0;

        for (var ownGoals = 0; ownGoals <= PoissonGoalCutoff; ownGoals++)
        {
            for (var opponentGoals = 0; opponentGoals <= PoissonGoalCutoff; opponentGoals++)
            {
                var probability = own[ownGoals] * opponent[opponentGoals];
                if (ownGoals > opponentGoals) win += probability;
                else if (ownGoals == opponentGoals) draw += probability;
                else loss += probability;
            }
        }

        var total = Math.Max(1e-12, win + draw + loss);
        return (win / total, draw / total, loss / total);
    }

    private static double[] PoissonDistribution(double lambda, int maxGoals)
    {
        var probabilities = new double[maxGoals + 1];
        probabilities[0] = Math.Exp(-lambda);
        for (var goals = 1; goals <= maxGoals; goals++)
            probabilities[goals] = probabilities[goals - 1] * lambda / goals;
        return probabilities;
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
