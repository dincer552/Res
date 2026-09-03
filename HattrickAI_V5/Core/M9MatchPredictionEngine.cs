using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: M8 chance allocation -> sector resolution -> documented event layer -> goals -> W/D/L.
/// Explicit mechanisms are taken from the 2026 Hattrick research paper; hidden inputs remain explicit calibration gaps.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double PaperSetPieceShare = 0.1255;
    private const double SetPieceNeutralConversion = 0.5;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, RegionalRatingSnapshot opponent, MatchLocation location, IReadOnlyList<Player>? players = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(chance);
        ArgumentNullException.ThrowIfNull(opponent);

        var ownLeft = chance.LeftAttackVsRightDefence;
        var ownCentre = chance.CentreAttackVsCentreDefence;
        var ownRight = chance.RightAttackVsLeftDefence;
        var opponentLeft = chance.OpponentLeftAttackVsOwnRightDefence;
        var opponentCentre = chance.OpponentCentreAttackVsOwnCentreDefence;
        var opponentRight = chance.OpponentRightAttackVsOwnLeftDefence;
        var ownRegularQuality = WeightedRegularQuality(ownLeft, ownCentre, ownRight, chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);
        var opponentRegularQuality = WeightedRegularQuality(opponentLeft, opponentCentre, opponentRight, chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);

        var totalRegularChances = Math.Max(1e-9, chance.OwnRegularChanceExpected + chance.OpponentRegularChanceExpected);
        var ownChanceShare = Clamp01(chance.OwnRegularChanceExpected / totalRegularChances);
        var opponentChanceShare = Clamp01(chance.OpponentRegularChanceExpected / totalRegularChances);

        // M8 owns tactical opportunity volumes. Long Shots are removed from the
        // normal LMR bucket here exactly once; LS goal conversion remains pending
        // because the paper publishes it as a plotted relationship rather than a
        // closed-form equation.
        var ownNormalChanceVolume = chance.NormalRegularChanceExpectedAfterLongShots;
        var ownNormalGoals = ownNormalChanceVolume * ownRegularQuality;

        // M8 owns CA opportunity generation. M9 resolves the resulting opportunity
        // against the sector quality instead of regenerating the CA volume.
        var counterAttackGoals = chance.CounterAttackChanceExpected * ownRegularQuality;

        var ownSetPieceExpected = 10.0 * PaperSetPieceShare * ownChanceShare;
        var opponentSetPieceExpected = 10.0 * PaperSetPieceShare * opponentChanceShare;
        var ownSetPieceGoals = ownSetPieceExpected * SetPieceNeutralConversion;
        var opponentSetPieceGoals = opponentSetPieceExpected * SetPieceNeutralConversion;

        var events = players is not null && players.Count > 0
            ? new M9EventGoalEngine().Calculate(
                candidate.Lineup,
                players,
                candidate.Rating.Midfield,
                opponent.Midfield,
                chance.Tactic,
                chance.CreativeEventMultiplier,
                ownNormalChanceVolume,
                chance.OpponentRegularChanceExpected,
                opponentRegularQuality,
                opponentCentralDefenders: 3)
            : M9EventGoalBreakdown.Empty;

        // PDIM suppresses opponent Normal opportunities. PNF extra attacks are
        // already represented in EventGoals.PowerfulNormalForwardGoals.
        var opponentNormalVolumeAfterPdim = chance.OpponentRegularChanceExpected * (1.0 - events.PressingSuppressionSignal);
        var opponentNormalGoals = opponentNormalVolumeAfterPdim * opponentRegularQuality;
        var ownSpecialGoals = events.PlayerBasedSpecialEventGoals + events.TeamBasedSpecialEventGoals + events.CounterAttackGoals + events.LongShotGoals + events.PowerfulNormalForwardGoals;
        var opponentSpecialGoals = events.ExpectedGoalsConcededFromOwnGoalEvents;
        var ownExpected = ClampGoals(ownNormalGoals + counterAttackGoals + ownSetPieceGoals + ownSpecialGoals);
        var opponentExpected = ClampGoals(opponentNormalGoals + opponentSetPieceGoals + opponentSpecialGoals);
        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);
        var prediction = new MatchPrediction(chance.MidfieldShare, ownExpected, opponentExpected, probabilities.Win, probabilities.Draw, probabilities.Loss);
        var structuralChance = Clamp01(ownChanceShare * ownRegularQuality + chance.SetPieceChanceShare * SetPieceNeutralConversion);

        return new M9PredictionResult(
            candidate.Lineup.Formation,
            CandidateId(candidate.Lineup),
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
            location,
            M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration)
        {
            EventGoals = events
        };
    }

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, MatchLocation location)
        => Predict(candidate, chance, InferOpponent(candidate), location, null);

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, RegionalRatingSnapshot opponent, MatchLocation location)
        => Predict(candidate, chance, opponent, location, null);

    private static RegionalRatingSnapshot InferOpponent(TacticalCandidate candidate)
    {
        var own = candidate.Rating;
        return new RegionalRatingSnapshot(
            InverseRating(own.LeftDefence, candidate.Matchup.RightDefenceMargin),
            InverseRating(own.CentralDefence, candidate.Matchup.CentralDefenceMargin),
            InverseRating(own.RightDefence, candidate.Matchup.LeftDefenceMargin),
            InverseRating(own.Midfield, candidate.Matchup.MidfieldMargin),
            InverseRating(own.LeftAttack, candidate.Matchup.RightAttackMargin),
            InverseRating(own.CentralAttack, candidate.Matchup.CentralAttackMargin),
            InverseRating(own.RightAttack, candidate.Matchup.LeftAttackMargin),
            0,0,0,0,0,0,0);
    }

    internal static (double Win, double Draw, double Loss) CalculatePoissonOutcomeProbabilities(double ownExpected, double opponentExpected)
    {
        ownExpected = ClampGoals(ownExpected); opponentExpected = ClampGoals(opponentExpected);
        var own = PoissonDistribution(ownExpected, PoissonGoalCutoff);
        var opponent = PoissonDistribution(opponentExpected, PoissonGoalCutoff);
        var win = 0.0; var draw = 0.0; var loss = 0.0;
        for (var ownGoals = 0; ownGoals <= PoissonGoalCutoff; ownGoals++)
        for (var opponentGoals = 0; opponentGoals <= PoissonGoalCutoff; opponentGoals++)
        {
            var p = own[ownGoals] * opponent[opponentGoals];
            if (ownGoals > opponentGoals) win += p; else if (ownGoals == opponentGoals) draw += p; else loss += p;
        }
        var total = Math.Max(1e-12, win + draw + loss);
        return (win / total, draw / total, loss / total);
    }

    private static double WeightedRegularQuality(double left, double centre, double right, double leftWeight, double centreWeight, double rightWeight)
    {
        var sum = leftWeight + centreWeight + rightWeight;
        return sum <= 0 ? 0.5 : Clamp01((left * leftWeight + centre * centreWeight + right * rightWeight) / sum);
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
    private static string CandidateId(Lineup lineup) => string.Join(";", lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
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
    public M9EventGoalBreakdown EventGoals { get; init; } = M9EventGoalBreakdown.Empty;
    public string PredictedResult => Prediction.WinProbability >= Prediction.LossProbability ? (Prediction.WinProbability >= Prediction.DrawProbability ? "Galibiyet" : "Beraberlik") : (Prediction.LossProbability >= Prediction.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");
    public string MostLikelyScore
    {
        get
        {
            var bestOwn = 0; var bestOpponent = 0; var bestProbability = double.MinValue;
            for (var own = 0; own <= 6; own++) for (var opponent = 0; opponent <= 6; opponent++)
            {
                var p = PoissonProbability(Prediction.ExpectedHomeGoals, own) * PoissonProbability(Prediction.ExpectedAwayGoals, opponent);
                if (p > bestProbability) { bestProbability = p; bestOwn = own; bestOpponent = opponent; }
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
        var p = Math.Exp(-Math.Max(0.05, lambda));
        for (var i = 1; i <= goals; i++) p *= lambda / i;
        return p;
    }
}

public enum M9CalibrationStatus { StructuralModelAwaitingHistoricalCalibration, CalibratedAgainstHistoricalMatches }
