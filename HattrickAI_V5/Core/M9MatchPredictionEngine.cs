using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: converts the M7/M8 matchup into a bounded match prediction.
/// The model follows the useful Hattrick match-engine structure: midfield
/// drives chance allocation, while sector attack/defence margins drive the
/// chance of converting those attacks. Home advantage is applied explicitly.
/// Historical calibration can replace coefficients later without changing M7/M8.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double BaseGoals = 0.35;
    private const double ChanceScale = 2.25;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    private const double MidfieldWeight = 0.25;
    private const double AttackWeight = 0.45;
    private const double DefenceWeight = 0.30;
    private const double MatchupLogitScale = 4.5;
    private const double StructuralChanceWeight = 0.20;
    private const double MatchupChanceWeight = 0.80;
    private const double HomeGoalBonus = 0.08;
    private const double AwayOpponentGoalBonus = 0.04;

    public M9PredictionResult Predict(
        TacticalCandidate candidate,
        M8ChanceResult chance,
        MatchLocation location)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(chance);

        var structuralChance = Clamp01(chance.StructuralChanceIndex);
        var midfieldShare = Clamp01(chance.MidfieldShare);

        var midfieldSignal = ClampSigned((midfieldShare * 2.0) - 1.0);
        var attackSignal = AverageSigned(
            chance.LeftAttackVsRightDefence,
            chance.CentreAttackVsCentreDefence,
            chance.RightAttackVsLeftDefence);
        var defenceSignal = AverageSigned(
            candidate.Matchup.LeftDefenceMargin,
            candidate.Matchup.CentralDefenceMargin,
            candidate.Matchup.RightDefenceMargin);

        var matchupSignal = ClampSigned(
            (MidfieldWeight * midfieldSignal) +
            (AttackWeight * attackSignal) +
            (DefenceWeight * defenceSignal));

        var matchupShare = LogisticShare(matchupSignal);
        var effectiveChance = Clamp01(
            (StructuralChanceWeight * structuralChance) +
            (MatchupChanceWeight * matchupShare));

        var ownExpected = ClampGoals(BaseGoals + ChanceScale * effectiveChance);
        var opponentExpected = ClampGoals(BaseGoals + ChanceScale * (1.0 - effectiveChance));

        // M7 already models the main venue/possession effect. Keep only a
        // small residual goal bonus here to avoid double-counting home advantage.
        if (location == MatchLocation.Home)
            ownExpected = ClampGoals(ownExpected + HomeGoalBonus);
        else if (location == MatchLocation.Away)
            opponentExpected = ClampGoals(opponentExpected + AwayOpponentGoalBonus);

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
            effectiveChance,
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
        for (var opponentGoals = 0; opponentGoals <= PoissonGoalCutoff; opponentGoals++)
        {
            var probability = own[ownGoals] * opponent[opponentGoals];
            if (ownGoals > opponentGoals) win += probability;
            else if (ownGoals == opponentGoals) draw += probability;
            else loss += probability;
        }

        var total = Math.Max(1e-12, win + draw + loss);
        return (win / total, draw / total, loss / total);
    }

    private static double AverageSigned(params double[] values)
        => values.Length == 0 ? 0 : ClampSigned(values.Average());

    private static double LogisticShare(double matchup)
    {
        var exponent = Math.Clamp(-MatchupLogitScale * matchup, -20.0, 20.0);
        return 1.0 / (1.0 + Math.Exp(exponent));
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
    M9CalibrationStatus CalibrationStatus)
{
    public string PredictedResult => Prediction.WinProbability >= Prediction.LossProbability
        ? (Prediction.WinProbability >= Prediction.DrawProbability ? "Galibiyet" : "Beraberlik")
        : (Prediction.LossProbability >= Prediction.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");

    public string MostLikelyScore
    {
        get
        {
            var bestOwn = 0;
            var bestOpponent = 0;
            var bestProbability = double.MinValue;
            for (var own = 0; own <= 6; own++)
            for (var opponent = 0; opponent <= 6; opponent++)
            {
                var probability = PoissonProbability(Prediction.ExpectedHomeGoals, own) * PoissonProbability(Prediction.ExpectedAwayGoals, opponent);
                if (probability > bestProbability)
                {
                    bestProbability = probability;
                    bestOwn = own;
                    bestOpponent = opponent;
                }
            }
            return $"{bestOwn}-{bestOpponent}";
        }
    }

    public string ConfidenceLabel
    {
        get
        {
            var top = Math.Max(Prediction.WinProbability, Math.Max(Prediction.DrawProbability, Prediction.LossProbability));
            return top >= 0.65 ? "Yüksek" : top >= 0.50 ? "Orta" : "Düşük";
        }
    }

    private static double PoissonProbability(double lambda, int goals)
    {
        var probability = Math.Exp(-Math.Max(0.05, lambda));
        for (var i = 1; i <= goals; i++) probability *= lambda / i;
        return probability;
    }
}

public enum M9CalibrationStatus
{
    StructuralModelAwaitingHistoricalCalibration,
    CalibratedAgainstHistoricalMatches
}
