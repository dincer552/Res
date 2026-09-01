using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M10: final deterministic decision layer. M10 does not alter the underlying
/// rating or matchup motors; it ranks already-evaluated candidates and returns
/// the single match plan that should be exposed to the caller.
/// </summary>
public sealed class M10FinalDecisionEngine
{
    public M10DecisionResult Select(
        IReadOnlyList<M10CandidateEvaluation> candidates,
        double tacticalWeight = 0.55,
        double predictionWeight = 0.30,
        double structuralWeight = 0.15)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new ArgumentException("M10 için en az bir aday gerekir.", nameof(candidates));

        if (tacticalWeight < 0 || predictionWeight < 0 || structuralWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(tacticalWeight));

        var weightTotal = tacticalWeight + predictionWeight + structuralWeight;
        if (weightTotal <= 0)
            throw new ArgumentException("M10 ağırlıklarının toplamı sıfırdan büyük olmalıdır.");

        var ranked = candidates
            .Where(IsValid)
            .Select(x => new RankedCandidate(
                x,
                CompositeScore(x, tacticalWeight, predictionWeight, structuralWeight, weightTotal)))
            .OrderByDescending(x => x.CompositeScore)
            .ThenByDescending(x => x.Candidate.TacticalCandidate.TacticalScore)
            .ThenBy(x => Signature(x.Candidate.TacticalCandidate.Lineup), StringComparer.Ordinal)
            .ToList();

        if (ranked.Count == 0)
            throw new InvalidOperationException("M10 geçerli bir aday değerlendirmesi bulamadı.");

        var winner = ranked[0].Candidate;
        var plan = new FinalMatchPlan(
            winner.TacticalCandidate.Lineup.Formation,
            winner.TacticalCandidate.Lineup,
            winner.TacticalCandidate.Rating,
            winner.TacticalCandidate.Matchup,
            winner.TacticalCandidate.TacticalScore);

        return new M10DecisionResult(
            plan,
            winner.Prediction,
            ranked.Select(x => new M10RankedCandidate(
                x.Candidate.TacticalCandidate.Lineup.Formation,
                Signature(x.Candidate.TacticalCandidate.Lineup),
                x.Candidate.TacticalCandidate.TacticalScore,
                x.Candidate.Prediction.WinProbability,
                x.CompositeScore)).ToList(),
            M10DecisionStatus.SelectedDeterministically);
    }

    private static bool IsValid(M10CandidateEvaluation x)
        => x.TacticalCandidate is not null &&
           x.Prediction is not null &&
           double.IsFinite(x.TacticalCandidate.TacticalScore);

    private static double CompositeScore(
        M10CandidateEvaluation candidate,
        double tacticalWeight,
        double predictionWeight,
        double structuralWeight,
        double totalWeight)
    {
        var tactical = candidate.TacticalCandidate.TacticalScore;
        var prediction = Math.Clamp(candidate.Prediction.WinProbability, 0.0, 1.0);
        var structural = Math.Clamp(candidate.StructuralScore, 0.0, 1.0);
        var tacticalNormalized = 1.0 / (1.0 + Math.Exp(-Math.Clamp(tactical, -20.0, 20.0)));

        return ((tacticalWeight * tacticalNormalized) +
                (predictionWeight * prediction) +
                (structuralWeight * structural)) / totalWeight;
    }

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
}

public sealed record M10CandidateEvaluation(
    TacticalCandidate TacticalCandidate,
    MatchPrediction Prediction,
    double StructuralScore);

public sealed record M10RankedCandidate(
    string Formation,
    string CandidateId,
    double TacticalScore,
    double WinProbability,
    double CompositeScore);

public sealed record M10DecisionResult(
    FinalMatchPlan BestPlan,
    MatchPrediction Prediction,
    IReadOnlyList<M10RankedCandidate> Ranking,
    M10DecisionStatus Status);

public enum M10DecisionStatus
{
    SelectedDeterministically,
    CalibrationRequired
}
