using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: M8 chance allocation -> sector resolution -> documented event layer -> goals -> W/D/L.
/// When opponent CHPP lineup + players are available, the same event engine is evaluated
/// from the opponent perspective so Specialty effects are symmetric instead of one-sided.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double PaperSetPieceShare = 0.1255;
    private const double SetPieceNeutralConversion = 0.5;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, RegionalRatingSnapshot opponent, MatchLocation location, IReadOnlyList<Player>? players = null, Lineup? opponentLineup = null, IReadOnlyList<Player>? opponentPlayers = null)
    {
        ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(chance); ArgumentNullException.ThrowIfNull(opponent);
        var ownLeft = chance.LeftAttackVsRightDefence; var ownCentre = chance.CentreAttackVsCentreDefence; var ownRight = chance.RightAttackVsLeftDefence;
        var opponentLeft = chance.OpponentLeftAttackVsOwnRightDefence; var opponentCentre = chance.OpponentCentreAttackVsOwnCentreDefence; var opponentRight = chance.OpponentRightAttackVsOwnLeftDefence;
        var ownRegularQuality = WeightedRegularQuality(ownLeft, ownCentre, ownRight, chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);
        var opponentRegularQuality = WeightedRegularQuality(opponentLeft, opponentCentre, opponentRight, chance.LeftChanceShare, chance.CentreChanceShare, chance.RightChanceShare);
        var totalRegularChances = Math.Max(1e-9, chance.OwnRegularChanceExpected + chance.OpponentRegularChanceExpected);
        var ownChanceShare = Clamp01(chance.OwnRegularChanceExpected / totalRegularChances); var opponentChanceShare = Clamp01(chance.OpponentRegularChanceExpected / totalRegularChances);
        var ownNormalChanceVolume = chance.NormalRegularChanceExpectedAfterLongShots; var ownCounterAttackGoals = chance.CounterAttackChanceExpected * ownRegularQuality;
        var ownSetPieceExpected = 10.0 * PaperSetPieceShare * ownChanceShare; var opponentSetPieceExpected = 10.0 * PaperSetPieceShare * opponentChanceShare;
        var ownSetPieceGoals = ownSetPieceExpected * SetPieceNeutralConversion; var opponentSetPieceGoals = opponentSetPieceExpected * SetPieceNeutralConversion;
        var ownEvents = players is not null && players.Count > 0
            ? new M9EventGoalEngine().Calculate(candidate.Lineup, players, candidate.Rating.Midfield, opponent.Midfield, chance.Tactic, chance.CreativeEventMultiplier, ownNormalChanceVolume, chance.OpponentRegularChanceExpected, ownRegularQuality, opponentRegularQuality, opponentCentralDefenders: CentralDefenderCount(opponentLineup, fallback: 3))
            : M9EventGoalBreakdown.Empty;
        var opponentEvents = opponentLineup is not null && opponentPlayers is not null && opponentPlayers.Count > 0
            ? new M9EventGoalEngine().Calculate(opponentLineup, opponentPlayers, opponent.Midfield, candidate.Rating.Midfield, chance.Tactic, chance.CreativeEventMultiplier, chance.OpponentRegularChanceExpected, ownNormalChanceVolume, opponentRegularQuality, ownRegularQuality, opponentCentralDefenders: CentralDefenderCount(candidate.Lineup, fallback: 3))
            : M9EventGoalBreakdown.Empty;
        var ownNormalVolumeAfterPdim = ownNormalChanceVolume * (1.0 - opponentEvents.PressingSuppressionSignal);
        var opponentNormalVolumeAfterPdim = chance.OpponentRegularChanceExpected * (1.0 - ownEvents.PressingSuppressionSignal);
        var ownNormalGoals = ownNormalVolumeAfterPdim * ownRegularQuality; var opponentNormalGoals = opponentNormalVolumeAfterPdim * opponentRegularQuality;
        var ownSpecialGoals = ownEvents.PlayerBasedSpecialEventGoals + ownEvents.TeamBasedSpecialEventGoals + ownEvents.CounterAttackGoals + ownEvents.LongShotGoals + ownEvents.PowerfulNormalForwardGoals + opponentEvents.ExpectedGoalsConcededFromOwnGoalEvents;
        var opponentSpecialGoals = opponentEvents.PlayerBasedSpecialEventGoals + opponentEvents.TeamBasedSpecialEventGoals + opponentEvents.CounterAttackGoals + opponentEvents.LongShotGoals + opponentEvents.PowerfulNormalForwardGoals + ownEvents.ExpectedGoalsConcededFromOwnGoalEvents;
        var ownExpected = ClampGoals(ownNormalGoals + ownCounterAttackGoals + ownSetPieceGoals + ownSpecialGoals); var opponentExpected = ClampGoals(opponentNormalGoals + opponentSetPieceGoals + opponentSpecialGoals);
        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);
        var prediction = new MatchPrediction(chance.MidfieldShare, ownExpected, opponentExpected, probabilities.Win, probabilities.Draw, probabilities.Loss) { Location = location, EventGoals = ownEvents };
        var structuralChance = Clamp01(ownChanceShare * ownRegularQuality + chance.SetPieceChanceShare * SetPieceNeutralConversion);
        return new M9PredictionResult(candidate.Lineup.Formation, CandidateId(candidate.Lineup), prediction, structuralChance, ownChanceShare, opponentChanceShare, ownRegularQuality, opponentRegularQuality, ownLeft, ownCentre, ownRight, opponentLeft, opponentCentre, opponentRight, location, M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration) { EventGoals = ownEvents, OpponentEventGoals = opponentEvents };
    }

    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, MatchLocation location) => Predict(candidate, chance, InferOpponent(candidate), location, null, null, null);
    public M9PredictionResult Predict(TacticalCandidate candidate, M8ChanceResult chance, RegionalRatingSnapshot opponent, MatchLocation location) => Predict(candidate, chance, opponent, location, null, null, null);

    private static RegionalRatingSnapshot InferOpponent(TacticalCandidate candidate)
    {
        var own = candidate.Rating;
        return new RegionalRatingSnapshot(InverseRating(own.LeftDefence, candidate.Matchup.RightDefenceMargin), InverseRating(own.CentralDefence, candidate.Matchup.CentralDefenceMargin), InverseRating(own.RightDefence, candidate.Matchup.LeftDefenceMargin), InverseRating(own.Midfield, candidate.Matchup.MidfieldMargin), InverseRating(own.LeftAttack, candidate.Matchup.RightAttackMargin), InverseRating(own.CentralAttack, candidate.Matchup.CentralAttackMargin), InverseRating(own.RightAttack, candidate.Matchup.LeftAttackMargin), 0,0,0,0,0,0,0);
    }

    internal static (double Win, double Draw, double Loss) CalculatePoissonOutcomeProbabilities(double ownExpected, double opponentExpected)
    {
        ownExpected = ClampGoals(ownExpected); opponentExpected = ClampGoals(opponentExpected); var own = PoissonDistribution(ownExpected, PoissonGoalCutoff); var opponent = PoissonDistribution(opponentExpected, PoissonGoalCutoff);
        var win = 0.0; var draw = 0.0; var loss = 0.0;
        for (var ownGoals = 0; ownGoals <= PoissonGoalCutoff; ownGoals++) for (var opponentGoals = 0; opponentGoals <= PoissonGoalCutoff; opponentGoals++) { var p = own[ownGoals] * opponent[opponentGoals]; if (ownGoals > opponentGoals) win += p; else if (ownGoals == opponentGoals) draw += p; else loss += p; }
        var total = Math.Max(1e-12, win + draw + loss); return (win / total, draw / total, loss / total);
    }

    private static double WeightedRegularQuality(double left, double centre, double right, double leftWeight, double centreWeight, double rightWeight)
    { var sum = leftWeight + centreWeight + rightWeight; return sum <= 0 ? 0.5 : Clamp01((left * leftWeight + centre * centreWeight + right * rightWeight) / sum); }
    private static double InverseRating(double own, double signedMargin)
    { var share = Clamp01((signedMargin + 1.0) * 0.5); if (share <= 0.001) return Math.Max(0.0, own * 1000.0); if (share >= 0.999) return 0.0; var logRatio = Math.Log(share / (1.0 - share)) / 1.5; return Math.Max(0.0, own / Math.Max(0.001, Math.Exp(logRatio))); }
    private static double[] PoissonDistribution(double lambda, int maxGoals)
    { var probabilities = new double[maxGoals + 1]; probabilities[0] = Math.Exp(-lambda); for (var goals = 1; goals <= maxGoals; goals++) probabilities[goals] = probabilities[goals - 1] * lambda / goals; return probabilities; }
    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
    private static double ClampGoals(double value) => Math.Clamp(value, 0.05, MaxGoals);
    private static string CandidateId(Lineup lineup) => string.Join(";", lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private static int CentralDefenderCount(Lineup? lineup, int fallback) => lineup is null ? fallback : Math.Clamp(lineup.Slots.Count(s => s.Code is "DEF-C" or "DEF-CL" or "DEF-CR"), 0, 3);
}

public sealed record M9PredictionResult(
    string Formation, string CandidateId, MatchPrediction Prediction,
    double StructuralChanceIndex, double OwnChanceShare, double OpponentChanceShare,
    double OwnAttackQuality, double OpponentAttackQuality,
    double OwnLeftAttackVsRightDefence, double OwnCentreAttackVsCentreDefence, double OwnRightAttackVsLeftDefence,
    double OpponentLeftAttackVsOwnRightDefence, double OpponentCentreAttackVsOwnCentreDefence, double OpponentRightAttackVsOwnLeftDefence,
    MatchLocation Location, M9CalibrationStatus CalibrationStatus)
{
    private M9EventGoalBreakdown _eventGoals = M9EventGoalBreakdown.Empty;
    private M9EventGoalBreakdown _opponentEventGoals = M9EventGoalBreakdown.Empty;
    private M9SimulationResult? _simulation;
    public M9EventGoalBreakdown EventGoals { get => _eventGoals.Contributions.Count > 0 ? _eventGoals : Prediction.EventGoals; init => _eventGoals = value; }
    public M9EventGoalBreakdown OpponentEventGoals { get => _opponentEventGoals; init => _opponentEventGoals = value; }
    public M9SimulationResult Simulation => _simulation ??= new M9SimulationEngine().Simulate(this);
    public string PredictedResult => Prediction.WinProbability >= Prediction.LossProbability ? (Prediction.WinProbability >= Prediction.DrawProbability ? "Galibiyet" : "Beraberlik") : (Prediction.LossProbability >= Prediction.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");
    public string MostLikelyScore
    {
        get { var bestOwn = 0; var bestOpponent = 0; var bestProbability = double.MinValue; for (var own = 0; own <= 6; own++) for (var opponent = 0; opponent <= 6; opponent++) { var p = PoissonProbability(Prediction.ExpectedHomeGoals, own) * PoissonProbability(Prediction.ExpectedAwayGoals, opponent); if (p > bestProbability) { bestProbability = p; bestOwn = own; bestOpponent = opponent; } } return $"{bestOwn}-{bestOpponent}"; }
    }
    public string ConfidenceLabel { get { var top = Math.Max(Prediction.WinProbability, Math.Max(Prediction.DrawProbability, Prediction.LossProbability)); return top >= 0.65 ? "Yüksek" : top >= 0.50 ? "Orta" : "Düşük"; } }
    private static double PoissonProbability(double lambda, int goals) { var p = Math.Exp(-Math.Max(0.05, lambda)); for (var i = 1; i <= goals; i++) p *= lambda / i; return p; }

    public static implicit operator M9PredictionResult(MatchPrediction prediction)
        => new(
            Formation: "Unknown",
            CandidateId: "match-prediction",
            Prediction: prediction,
            StructuralChanceIndex: 0.0,
            OwnChanceShare: prediction.PossessionProbability,
            OpponentChanceShare: 1.0 - prediction.PossessionProbability,
            OwnAttackQuality: 0.5,
            OpponentAttackQuality: 0.5,
            OwnLeftAttackVsRightDefence: 0.5,
            OwnCentreAttackVsCentreDefence: 0.5,
            OwnRightAttackVsLeftDefence: 0.5,
            OpponentLeftAttackVsOwnRightDefence: 0.5,
            OpponentCentreAttackVsOwnCentreDefence: 0.5,
            OpponentRightAttackVsOwnLeftDefence: 0.5,
            Location: prediction.Location,
            CalibrationStatus: M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration)
        { EventGoals = prediction.EventGoals };
}
