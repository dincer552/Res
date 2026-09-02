using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M11, ikinci aday database'inden gelen finalistleri son kez karşılaştırır.
/// M10 artık finali kilitlemez; M11 gerçek final selector'dür.
/// </summary>
public sealed class M11FinalSelectorEngine
{
    public M11DecisionResult Select(
        IReadOnlyList<M11CandidateEvaluation> candidates,
        int topRankingCount = 20)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) throw new ArgumentException("M11 için en az bir finalist gerekir.", nameof(candidates));

        var ranked = candidates
            .Where(IsValid)
            .Select(x => x with { FinalScore = FinalScore(x) })
            .OrderByDescending(x => x.FinalScore)
            .ThenByDescending(x => x.Prediction.WinProbability)
            .ThenByDescending(x => x.TacticalCandidate.TacticalScore)
            .ThenBy(x => x.TacticalCandidate.Lineup.Formation, StringComparer.Ordinal)
            .ThenBy(x => Signature(x.TacticalCandidate.Lineup), StringComparer.Ordinal)
            .ToList();

        if (ranked.Count == 0) throw new InvalidOperationException("M11 geçerli finalist bulamadı.");

        var winner = ranked[0];
        var plan = new FinalMatchPlan(
            winner.TacticalCandidate.Lineup.Formation,
            winner.TacticalCandidate.Lineup,
            winner.TacticalCandidate.Rating,
            winner.TacticalCandidate.Matchup,
            winner.TacticalCandidate.TacticalScore);

        return new M11DecisionResult(
            plan,
            winner.Prediction,
            ranked.Take(Math.Max(1, topRankingCount)).Select(x => new M11RankedCandidate(
                x.TacticalCandidate.Lineup.Formation,
                Signature(x.TacticalCandidate.Lineup),
                x.TacticalCandidate.TacticalScore,
                x.Prediction.WinProbability,
                x.FinalScore)).ToList(),
            ranked.Count,
            ranked.Select(x => x.TacticalCandidate.Lineup.Formation).Distinct(StringComparer.Ordinal).Count());
    }

    private static bool IsValid(M11CandidateEvaluation x)
        => x.TacticalCandidate is not null && x.Prediction is not null &&
           double.IsFinite(x.TacticalCandidate.TacticalScore) && double.IsFinite(x.Prediction.WinProbability);

    private static double FinalScore(M11CandidateEvaluation x)
    {
        var tactical = 1.0 / (1.0 + System.Math.Exp(-System.Math.Clamp(x.TacticalCandidate.TacticalScore, -20.0, 20.0)));
        var prediction = System.Math.Clamp(x.Prediction.WinProbability, 0.0, 1.0);
        var structural = System.Math.Clamp(x.StructuralScore, 0.0, 1.0);
        var stability = System.Math.Clamp(x.StabilityScore, 0.0, 1.0);
        return (0.45 * tactical) + (0.35 * prediction) + (0.15 * structural) + (0.05 * stability);
    }

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
}

public sealed record M11CandidateEvaluation(
    TacticalCandidate TacticalCandidate,
    MatchPrediction Prediction,
    double StructuralScore,
    double StabilityScore = 1.0);

public sealed record M11RankedCandidate(
    string Formation,
    string CandidateId,
    double TacticalScore,
    double WinProbability,
    double FinalScore);

public sealed record M11DecisionResult(
    FinalMatchPlan BestPlan,
    MatchPrediction Prediction,
    IReadOnlyList<M11RankedCandidate> Ranking,
    int CandidateCount,
    int FormationCount);
