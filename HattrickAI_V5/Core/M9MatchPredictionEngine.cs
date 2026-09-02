using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: rating-merkezli maç çekirdeği.
/// Oyuncu seviyesi M7'de takımın 7 bölgesel ratingine indirgenir; M9 bu
/// kanonik ratingleri kullanarak midfield -> chance -> sector attack/defence
/// akışını çözer. Rakibin hücum riski ayrı hesaplanır ve aynı xG çifti üzerinden
/// W/D/L üretilir. Ardından M9SimulationEngine aynı çekirdeği 1000 kez farklı
/// venue/chance varyantlarıyla çalıştırarak Monte Carlo dağılımı üretir.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double BaseGoals = 0.20;
    private const double GoalScale = 2.80;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    private const double CentreChanceWeight = 0.35;
    private const double SideChanceWeight = 0.25;
    private const double SetPieceChanceWeight = 0.15;
    private const double ExclusiveChanceFloor = 0.35;
    private const double SectorBreakthroughScale = 1.5;

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

        // 1) MIDFIELD -> CHANCE VOLUME
        // Venue/attitude etkisi M7 RatingContext içinde uygulanır; M9 tekrar bonus eklemez.
        var ownChanceShare = Clamp01(chance.MidfieldShare);
        var opponentChanceShare = 1.0 - ownChanceShare;
        var ownChanceVolume = ExclusiveChanceFloor + ((1.0 - ExclusiveChanceFloor) * ownChanceShare);
        var opponentChanceVolume = ExclusiveChanceFloor + ((1.0 - ExclusiveChanceFloor) * opponentChanceShare);

        // 2) OWN ATTACK -> OPPONENT DEFENCE
        // Sol -> rakip sağ savunma, merkez -> rakip merkez savunma,
        // sağ -> rakip sol savunma. M8 mapping ile aynıdır.
        var ownLeft = SectorBreakthrough(own.LeftAttack, opponent.RightDefence);
        var ownCentre = SectorBreakthrough(own.CentralAttack, opponent.CentralDefence);
        var ownRight = SectorBreakthrough(own.RightAttack, opponent.LeftDefence);
        var ownAttackQuality = WeightedAttackQuality(
            ownLeft,
            ownCentre,
            ownRight,
            chance.LeftChanceShare,
            chance.CentreChanceShare,
            chance.RightChanceShare,
            chance.SetPieceChanceShare);

        // 3) OPPONENT ATTACK -> OWN DEFENCE
        var opponentLeft = SectorBreakthrough(opponent.LeftAttack, own.RightDefence);
        var opponentCentre = SectorBreakthrough(opponent.CentralAttack, own.CentralDefence);
        var opponentRight = SectorBreakthrough(opponent.RightAttack, own.LeftDefence);
        var opponentAttackQuality = WeightedAttackQuality(
            opponentLeft,
            opponentCentre,
            opponentRight,
            SideChanceWeight,
            CentreChanceWeight,
            SideChanceWeight,
            SetPieceChanceWeight);

        var structuralChance = Clamp01(chance.StructuralChanceIndex);

        // M9 rating-merkezlidir: midfield fırsat hacmini, sektör ratingleri
        // fırsatın gole dönüşme kalitesini belirler. M8 structural index burada
        // tekrar xG'ye eklenmez; aynı bilgi iki kez sayılmaz.
        var ownExpected = ClampGoals(BaseGoals + GoalScale * ownChanceVolume * ownAttackQuality);
        var opponentExpected = ClampGoals(BaseGoals + GoalScale * opponentChanceVolume * opponentAttackQuality);

        // Home advantage M7 rating katmanında zaten uygulanıyor.
        _ = location;

        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);
        var prediction = new MatchPrediction(
            ownChanceShare,
            ownExpected,
            opponentExpected,
            probabilities.Win,
            probabilities.Draw,
            probabilities.Loss);

        return BuildResult(
            candidate.Lineup.Formation,
            candidate.Lineup,
            prediction,
            structuralChance,
            ownChanceShare,
            opponentChanceShare,
            ownAttackQuality,
            opponentAttackQuality,
            ownLeft,
            ownCentre,
            ownRight,
            opponentLeft,
            opponentCentre,
            opponentRight,
            location);
    }

    /// <summary>
    /// Eski offline regression çağrıları için geriye dönük uyumluluk.
    /// Yeni pipeline her zaman gerçek opponent 7-ratingini vermelidir.
    /// </summary>
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

    private static M9PredictionResult BuildResult(
        string formation,
        Lineup lineup,
        MatchPrediction prediction,
        double structuralChance,
        double ownChanceShare,
        double opponentChanceShare,
        double ownAttackQuality,
        double opponentAttackQuality,
        double ownLeft,
        double ownCentre,
        double ownRight,
        double opponentLeft,
        double opponentCentre,
        double opponentRight,
        MatchLocation location)
    {
        var result = new M9PredictionResult(
            formation,
            CandidateId(lineup),
            prediction,
            structuralChance,
            ownChanceShare,
            opponentChanceShare,
            ownAttackQuality,
            opponentAttackQuality,
            ownLeft,
            ownCentre,
            ownRight,
            opponentLeft,
            opponentCentre,
            opponentRight,
            location,
            M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration);

        // 1000x Monte Carlo: M9 çekirdeğinin farklı chance/venue varyantlarındaki
        // davranışını tekrarlar. Seed deterministiktir; aynı aday aynı sonucu verir.
        return result with { Simulation = new M9SimulationEngine().Simulate(result) };
    }

    private static double WeightedAttackQuality(
        double left,
        double centre,
        double right,
        double leftWeight,
        double centreWeight,
        double rightWeight,
        double setPieceWeight)
    {
        var regularWeight = leftWeight + centreWeight + rightWeight;
        var weightedRegular = regularWeight <= 0
            ? 0.5
            : ((left * leftWeight) + (centre * centreWeight) + (right * rightWeight)) / regularWeight;

        return Clamp01((regularWeight * weightedRegular) + (setPieceWeight * 0.5));
    }

    private static double SectorBreakthrough(double attack, double defence)
    {
        var safeAttack = Math.Max(0.001, attack);
        var safeDefence = Math.Max(0.001, defence);
        var logRatio = Math.Log(safeAttack / safeDefence);
        return Clamp01(1.0 / (1.0 + Math.Exp(-SectorBreakthroughScale * logRatio)));
    }

    private static double InverseRating(double own, double signedMargin)
    {
        var share = Clamp01((signedMargin + 1.0) * 0.5);
        if (share <= 0.001) return Math.Max(0.0, own * 1000.0);
        if (share >= 0.999) return 0.0;

        var logRatio = Math.Log(share / (1.0 - share)) / SectorBreakthroughScale;
        var ratio = Math.Exp(logRatio);
        return Math.Max(0.0, own / Math.Max(0.001, ratio));
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
    double OwnChanceShare,
    double OpponentChanceShare,
    double OwnAttackQuality,
    double OpponentAttackQuality,
    double OwnLeftAttackVsRightDefence,
    double OwnCentreAttackVsCentreDefence,
    double OwnRightAttackVsLeftDefence,
    double OpponentLeftAttackVsOwnRightDefence,
    double OpponentCentreAttackVsOwnCentreDefence,
    double OpponentRightAttackVsOwnLeftDefence,
    MatchLocation Location,
    M9CalibrationStatus CalibrationStatus)
{
    // Monte Carlo database sonucu. Ham 1000 kayıt veriyi API'ye şişirmemesi için
    // özet dağılımlar public, ham kayıtlar internal tutulur.
    public M9SimulationResult? Simulation { get; init; }

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
