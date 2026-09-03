using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M11, ikinci aday database'inden gelen finalistleri son kez karşılaştırır.
/// Final ranking uses the event-based Monte Carlo outcome distribution and keeps
/// tactical/structural quality as supporting signals.
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
            .Select(x => new RankedFinalist(x, FinalScore(x)))
            .OrderByDescending(x => x.FinalScore)
            .ThenByDescending(x => MonteCarloWinProbability(x.Candidate.Prediction))
            .ThenByDescending(x => x.Candidate.TacticalCandidate.TacticalScore)
            .ThenBy(x => x.Candidate.TacticalCandidate.Lineup.Formation, StringComparer.Ordinal)
            .ThenBy(x => Signature(x.Candidate.TacticalCandidate.Lineup), StringComparer.Ordinal)
            .ToList();

        if (ranked.Count == 0) throw new InvalidOperationException("M11 geçerli finalist bulamadı.");

        var winner = ranked[0].Candidate;
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
                x.Candidate.TacticalCandidate.Lineup.Formation,
                Signature(x.Candidate.TacticalCandidate.Lineup),
                x.Candidate.TacticalCandidate.TacticalScore,
                MonteCarloWinProbability(x.Candidate.Prediction),
                x.FinalScore)).ToList(),
            ranked.Count,
            ranked.Select(x => x.Candidate.TacticalCandidate.Lineup.Formation).Distinct(StringComparer.Ordinal).Count());
    }

    private static bool IsValid(M11CandidateEvaluation x)
        => x.TacticalCandidate is not null && x.Prediction is not null &&
           double.IsFinite(x.TacticalCandidate.TacticalScore) && double.IsFinite(x.Prediction.WinProbability);

    private static double MonteCarloWinProbability(MatchPrediction prediction)
        => System.Math.Clamp(prediction.Simulation.Outcome.WinProbability, 0.0, 1.0);

    private static double FinalScore(M11CandidateEvaluation x)
    {
        var tactical = 1.0 / (1.0 + System.Math.Exp(-System.Math.Clamp(x.TacticalCandidate.TacticalScore, -20.0, 20.0)));
        var simulation = x.Prediction.Simulation.Outcome;
        var win = System.Math.Clamp(simulation.WinProbability, 0.0, 1.0);
        var draw = System.Math.Clamp(simulation.DrawProbability, 0.0, 1.0);
        var structural = System.Math.Clamp(x.StructuralScore, 0.0, 1.0);
        var stability = System.Math.Clamp(x.StabilityScore, 0.0, 1.0);
        var riskAdjustedOutcome = win + (0.50 * draw);
        return (0.35 * tactical) + (0.35 * win) + (0.15 * structural) + (0.05 * stability) + (0.10 * riskAdjustedOutcome);
    }

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));

    private sealed record RankedFinalist(M11CandidateEvaluation Candidate, double FinalScore);
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
