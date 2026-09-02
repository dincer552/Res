using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9: rating-merkezli maç çekirdeği.
/// Oyuncu seviyesi M7'de takımın 7 bölgesel ratingine indirgenir; M9 bu
/// kanonik ratingleri kullanarak midfield -> chance -> sector attack/defence
/// akışını çözer. Rakibin hücum riski ayrı hesaplanır ve aynı xG çifti üzerinden
/// W/D/L üretilir. M8 yapısal şans indeksi yalnızca teşhis/kalibrasyon girdisidir;
/// ana maç olasılığını ikinci kez şişirmez.
/// </summary>
public sealed class M9MatchPredictionEngine
{
    private const double BaseGoals = 0.25;
    private const double GoalScale = 3.0;
    private const double MaxGoals = 5.0;
    private const int PoissonGoalCutoff = 20;

    // Hattrick çekirdeğinde regular chance dağılımının temel ağırlıkları:
    // merkez %35, sol/sağ %25, duran top %15.
    private const double CentreChanceWeight = 0.35;
    private const double SideChanceWeight = 0.25;
    private const double SetPieceChanceWeight = 0.15;

    // M8 structural index sonucu yeniden üretmek için kullanılmaz. Yalnızca
    // modelin dışarıdan gelen yapısal sinyali ne kadar gördüğünü raporlamak için
    // düşük ağırlıklı yardımcı bir diagnostik olarak tutulur.

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
        // M7, venue/attitude dahil rating bağlamını zaten uyguladığı için M9
        // ikinci kez home bonus eklemez. Böylece ev avantajı double-count edilmez.
        var ownChanceShare = Clamp01(chance.MidfieldShare);
        var opponentChanceShare = 1.0 - ownChanceShare;

        // 2) OWN ATTACK -> OPPONENT DEFENCE
        // Sol -> rakip sağ savunma, merkez -> rakip merkez savunma,
        // sağ -> rakip sol savunma. Bu mapping M8 ile birebir korunur.
        var ownLeft = Share(own.LeftAttack, opponent.RightDefence);
        var ownCentre = Share(own.CentralAttack, opponent.CentralDefence);
        var ownRight = Share(own.RightAttack, opponent.LeftDefence);
        var ownAttackQuality = WeightedAttackQuality(
            ownLeft,
            ownCentre,
            ownRight,
            chance.LeftChanceShare,
            chance.CentreChanceShare,
            chance.RightChanceShare,
            chance.SetPieceChanceShare);

        // 3) OPPONENT ATTACK -> OWN DEFENCE
        // Rakibin tehdidi ayrı hesaplanır; M9 sadece "biz kaç gol atarız?"
        // sorusuna değil, "rakip hangi koridordan bizi deler?" sorusuna da bakar.
        var opponentLeft = Share(opponent.LeftAttack, own.RightDefence);
        var opponentCentre = Share(opponent.CentralAttack, own.CentralDefence);
        var opponentRight = Share(opponent.RightAttack, own.LeftDefence);
        var opponentAttackQuality = WeightedAttackQuality(
            opponentLeft,
            opponentCentre,
            opponentRight,
            SideChanceWeight,
            CentreChanceWeight,
            SideChanceWeight,
            SetPieceChanceWeight);

        // M8'deki dağılım kendi taktiğinin sonucudur. Rakip için doğrudan bir
        // taktik girdisi yoksa Hattrick'in temel 35/25/25/15 dağılımını kullan.
        // Set-piece quality için rating katmanında ayrı bir FK ratingi olmadığı
        // için nötr 0.50 kullanılır.
        ownAttackQuality = Clamp01(ownAttackQuality);
        opponentAttackQuality = Clamp01(opponentAttackQuality);

        // M8 structural index yalnızca kalibrasyon görünürlüğü sağlar; M9'un
        // beklenen gollerini aynı bilgiyi iki kez kullanarak bozmaz.
        var structuralChance = Clamp01(chance.StructuralChanceIndex);

        var ownExpected = ClampGoals(BaseGoals + GoalScale * ownChanceShare * ownAttackQuality);
        var opponentExpected = ClampGoals(BaseGoals + GoalScale * opponentChanceShare * opponentAttackQuality);

        // location bilinçli olarak sayısal bonus üretmiyor: Home/Away etkisi
        // M7 rating context içinde zaten uygulanıyor. Burada sadece model
        // sözleşmesinin explicit kalmasını sağlıyoruz.
        _ = location;

        var probabilities = CalculatePoissonOutcomeProbabilities(ownExpected, opponentExpected);
        var prediction = new MatchPrediction(
            ownChanceShare,
            ownExpected,
            opponentExpected,
            probabilities.Win,
            probabilities.Draw,
            probabilities.Loss);

        return new M9PredictionResult(
            candidate.Lineup.Formation,
            CandidateId(candidate.Lineup),
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

    private static double Share(double own, double opponent)
    {
        var ownSafe = Math.Max(0, own);
        var opponentSafe = Math.Max(0, opponent);
        var total = ownSafe + opponentSafe;
        return total <= 0 ? 0.5 : Clamp01(ownSafe / total);
    }

    private static double PoissonDistributionValue(double lambda, int goals)
    {
        var probability = Math.Exp(-Math.Max(0.05, lambda));
        for (var i = 1; i <= goals; i++) probability *= lambda / i;
        return probability;
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
        => PoissonProbabilityCore(lambda, goals);

    private static double PoissonProbabilityCore(double lambda, int goals)
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
