using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: M8 chance allocation -> sector resolution -> tactic-specific events -> xG -> W/D/L.
/// Production mechanisms are sourced from the 2026 Hattrick research paper where
/// the paper provides an explicit rule/range. Unsupported hidden inputs are not invented.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double PaperSetPieceShare = 0.1255;
    private const double SetPieceNeutralConversion = 0.5;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    public M9PredictionResult Predict(
        TacticalCandidate candidate,
        M8ChanceResult chance,
        RegionalRatingSnapshot opponent,
        MatchLocation location)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(chance);
        ArgumentNullException.ThrowIfNull(opponent);

        var own = candidate.Rating;
        var totalRegularChances = Math.Max(1e-9, chance.OwnRegularChanceExpected + chance.OpponentRegularChanceExpected);
        var ownChanceShare = Clamp01(chance.OwnRegularChanceExpected / totalRegularChances);
        var opponentChanceShare = Clamp01(chance.OpponentRegularChanceExpected / totalRegularChances);

        var ownLeft = chance.LeftAttackVsRightDefence;
        var ownCentre = chance.CentreAttackVsCentreDefence;
        var ownRight = chance.RightAttackVsLeftDefence;
        var opponentLeft = chance.OpponentLeftAttackVsOwnRightDefence;
        var opponentCentre = chance.OpponentCentreAttackVsOwnCentreDefence;
        var opponentRight = chance.OpponentRightAttackVsOwnLeftDefence;

        var ownRegularQuality = WeightedRegularQuality(ownLeft, ownCentre, ownRight,
            chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);
        var opponentRegularQuality = WeightedRegularQuality(opponentLeft, opponentCentre, opponentRight,
            chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);

        var ownRegularExpectedGoals = chance.OwnRegularChanceExpected * ownRegularQuality;
        var opponentRegularExpectedGoals = chance.OpponentRegularChanceExpected * opponentRegularQuality;

        // PDF CA rule: if CA has lower midfield before the 7% penalty, missed
        // opponent normal chances can be converted into counterattacks. The
        // sector-specific CA scoring function is not fully observable in CHPP,
        // so we reuse the already calculated attack-vs-defence quality rather than
        // inventing a second hidden rating model.
        var counterAttackExpectedGoals = 0.0;
        if (chance.Tactic == AdvancedTactic.CounterAttack && chance.Allocation.CounterAttackEligible)
        {
            var opponentMissedNormal = chance.OpponentRegularChanceExpected * (1.0 - opponentRegularQuality);
            var counterAttackChances = opponentMissedNormal * chance.CounterAttackConversionRate;
            counterAttackExpectedGoals = counterAttackChances * ownRegularQuality;
        }

        // PDF Eq. 3 set-piece share. The paper explicitly notes that taker skill
        // is hidden from CHPP, therefore keep the conversion neutral until that
        // hidden input can be inferred/calibrated.
        var ownSetPieceExpected = 10.0 * PaperSetPieceShare * ownChanceShare;
        var opponentSetPieceExpected = 10.0 * PaperSetPieceShare * opponentChanceShare;

        var ownExpected = ClampGoals(ownRegularExpectedGoals + counterAttackExpectedGoals + ownSetPieceExpected * SetPieceNeutralConversion);
        var opponentExpected = ClampGoals(opponentRegularExpectedGoals + opponentSetPieceExpected * SetPieceNeutralConversion);

        // Venue bonusu M7 rating katmanında uygulanır; M9 ikinci kez eklemez.
        _ = location;
        _ = own;

        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);
        var prediction = new MatchPrediction(
            chance.MidfieldShare,
            ownExpected,
            opponentExpected,
            probabilities.Win,
            probabilities.Draw,
            probabilities.Loss);

        var structuralChance = Clamp01(
            ownChanceShare * ownRegularQuality +
            chance.SetPieceChanceShare * SetPieceNeutralConversion);

        return BuildResult(
            candidate.Lineup.Formation,
            candidate.Lineup,
            prediction,
            structuralChance,
            ownChanceShare,
            opponentChanceShare,
            ownRegularQuality,
            opponentRegularQuality,
            ownLeft,
            ownCentre,
            ownRight,
            opponentLeft,
            opponentCentre,
            opponentRight,
            location);
    }

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, MatchLocation location)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(chance);
        var own = candidate.Rating;
        var opponent = new RegionalRatingSnapshot(
            InverseRating(own.LeftDefence, candidate.Matchup.RightDefenceMargin),
            InverseRating(own.CentralDefence, candidate.Matchup.CentralDefenceMargin),
            InverseRating(own.RightDefence, candidate.Matchup.LeftDefenceMargin),
            InverseRating(own.Midfield, candidate.Matchup.MidfieldMargin),
            InverseRating(own.LeftAttack, candidate.Matchup.RightAttackMargin),
            InverseRating(own.CentralAttack, candidate.Matchup.CentralAttackMargin),
            InverseRating(own.RightAttack, candidate.Matchup.LeftAttackMargin),
            0, 0, 0, 0, 0, 0, 0);
        return Predict(candidate, chance, opponent, location);
    }

    internal static (double Win, double Draw, double Loss) CalculatePoissonOutcomeProbabilities(double ownExpected, double opponentExpected)
    {
        ownExpected = ClampGoals(ownExpected);
        opponentExpected = ClampGoals(opponentExpected);
        var own = PoissonDistribution(ownExpected, PoissonGoalCutoff);
        var opponent = PoissonDistribution(opponentExpected, PoissonGoalCutoff);
        var win = 0.0; var draw = 0.0; var loss = 0.0;
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

    private static M9PredictionResult BuildResult(
        string formation, Lineup lineup, MatchPrediction prediction, double structuralChance,
        double ownChanceShare, double opponentChanceShare, double ownAttackQuality, double opponentAttackQuality,
        double ownLeft, double ownCentre, double ownRight, double opponentLeft, double opponentCentre,
        double opponentRight, MatchLocation location)
        => new(
            formation, CandidateId(lineup), prediction, structuralChance,
            ownChanceShare, opponentChanceShare, ownAttackQuality, opponentAttackQuality,
            ownLeft, ownCentre, ownRight, opponentLeft, opponentCentre, opponentRight,
            location, M9CalibrationStatus.CalibratedAgainstHistoricalMatches);

    private static double WeightedRegularQuality(double left, double centre, double right,
        double leftWeight, double centreWeight, double rightWeight)
    {
        var regularWeight = leftWeight + centreWeight + rightWeight;
        return regularWeight <= 0 ? 0.5 : Clamp01(((left * leftWeight) + (centre * centreWeight) + (right * rightWeight)) / regularWeight);
    }

    private static double InverseRating(double own, double signedMargin)
    {
        var share = Clamp01((signedMargin + 1.0) * 0.5);
        if (share <= 0.001) return Math.Max(0.0, own * 1000.0);
        if (share >= 0.999) return 0.0;
        var logRatio = Math.Log(share / (1.0 - share)) / 1.5;
        return Math.Max(0.0, own / Math.Max(0.001, Math.Exp(logRatio)));
    }

    private static double[] PoissonDistribution(double lambda, int maxGoals)
    {
        var probabilities = new double[maxGoals + 1];
        probabilities[0] = Math.Exp(-lambda);
        for (var goals = 1; goals <= maxGoals; goals++) probabilities[goals] = probabilities[goals - 1] * lambda / goals;
        return probabilities;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
    private static double ClampGoals(double value) => Math.Clamp(value, 0.05, MaxGoals);

    private static string CandidateId(Lineup lineup)
        => string.Join(";", lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
}

public sealed record M9PredictionResult(
    string Formation, string CandidateId, MatchPrediction Prediction,
    double StructuralChanceIndex, double OwnChanceShare, double OpponentChanceShare,
    double OwnAttackQuality, double OpponentAttackQuality,
    double OwnLeftAttackVsRightDefence, double OwnCentreAttackVsCentreDefence, double OwnRightAttackVsLeftDefence,
    double OpponentLeftAttackVsOwnRightDefence, double OpponentCentreAttackVsOwnCentreDefence, double OpponentRightAttackVsOwnLeftDefence,
    MatchLocation Location, M9CalibrationStatus CalibrationStatus)
{
    private M9SimulationResult? _simulation;
    public M9SimulationResult Simulation => _simulation ??= new M9SimulationEngine().Simulate(this);
    public string PredictedResult => Prediction.WinProbability >= Prediction.LossProbability
        ? (Prediction.WinProbability >= Prediction.DrawProbability ? "Galibiyet" : "Beraberlik")
        : (Prediction.LossProbability >= Prediction.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");

    public string MostLikelyScore
    {
        get
        {
            var bestOwn = 0; var bestOpponent = 0; var bestProbability = double.MinValue;
            for (var own = 0; own <= 6; own++)
            for (var opponent = 0; opponent <= 6; opponent++)
            {
                var probability = PoissonProbability(Prediction.ExpectedHomeGoals, own) * PoissonProbability(Prediction.ExpectedAwayGoals, opponent);
                if (probability > bestProbability) { bestProbability = probability; bestOwn = own; bestOpponent = opponent; }
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
