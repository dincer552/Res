using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M10: formation-aware candidate review and deterministic decision layer.
/// It does not hide search depth; every represented formation receives an
/// explicit rank, candidate count, depth status and margin versus the next one.
/// </summary>
public sealed class M10FinalDecisionEngine
{
    public const int RequiredFormationDepth = CandidateEvaluationDatabase.MinimumPerFormation;

    public M10DecisionResult Select(
        IReadOnlyList<M10CandidateEvaluation> candidates,
        double tacticalWeight = 0.55,
        double predictionWeight = 0.30,
        double structuralWeight = 0.15)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new ArgumentException("M10 için en az bir aday gerekir.", nameof(candidates));

        var ranked = candidates
            .Where(IsValid)
            .Select(x => new RankedCandidate(
                x,
                CompositeScore(x.TacticalCandidate.TacticalScore, x.Prediction.WinProbability, x.StructuralScore,
                    tacticalWeight, predictionWeight, structuralWeight)))
            .OrderByDescending(x => x.CompositeScore)
            .ThenByDescending(x => x.Candidate.TacticalCandidate.TacticalScore)
            .ThenBy(x => Signature(x.Candidate.TacticalCandidate.Lineup), StringComparer.Ordinal)
            .ToList();

        if (ranked.Count == 0)
            throw new InvalidOperationException("M10 geçerli bir aday değerlendirmesi bulamadı.");

        var formationGroups = ranked
            .GroupBy(x => x.Candidate.TacticalCandidate.Lineup.Formation, StringComparer.Ordinal)
            .Select(group => new
            {
                Formation = group.Key,
                Best = group.First(),
                Candidates = group.Count()
            })
            .OrderByDescending(x => x.Best.CompositeScore)
            .ThenByDescending(x => x.Best.Candidate.TacticalCandidate.TacticalScore)
            .ThenBy(x => x.Formation, StringComparer.Ordinal)
            .ToList();

        var formationCompetition = formationGroups
            .Select((group, index) =>
            {
                var nextScore = index + 1 < formationGroups.Count
                    ? formationGroups[index + 1].Best.CompositeScore
                    : 0d;
                var margin = index + 1 < formationGroups.Count
                    ? group.Best.CompositeScore - nextScore
                    : 0d;
                return new M10FormationCompetition(
                    group.Formation,
                    Signature(group.Best.Candidate.TacticalCandidate.Lineup),
                    group.Best.Candidate.TacticalCandidate.TacticalScore,
                    group.Best.Candidate.Prediction.WinProbability,
                    group.Best.CompositeScore,
                    group.Candidates)
                {
                    Rank = index + 1,
                    MarginVsNext = margin,
                    SearchDepthStatus = group.Candidates >= RequiredFormationDepth
                        ? M10SearchDepthStatus.Sufficient
                        : M10SearchDepthStatus.Insufficient
                };
            })
            .ToList();

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
            M10DecisionStatus.SelectedDeterministically)
        {
            FormationCompetition = formationCompetition
        };
    }

    /// <summary>
    /// Auto mode: M10 compares the three legal competitive-match attitudes for
    /// the already selected XI using the same composite scoring model.
    /// </summary>
    public M10ApproachDecision SelectApproach(
        IReadOnlyList<M10ApproachEvaluation> candidates,
        double tacticalWeight = 0.55,
        double predictionWeight = 0.30,
        double structuralWeight = 0.15)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var ranked = candidates
            .Where(IsValidApproach)
            .Select(x => new RankedApproach(
                x,
                CompositeScore(x.TacticalCandidate.TacticalScore, x.Prediction.WinProbability, x.StructuralScore,
                    tacticalWeight, predictionWeight, structuralWeight)))
            .OrderByDescending(x => x.CompositeScore)
            .ThenBy(x => ApproachOrder(x.Approach.Attitude))
            .ToList();

        if (ranked.Count == 0)
            throw new InvalidOperationException("M10 Auto için geçerli bir maç yaklaşımı değerlendirilemedi.");

        var winner = ranked[0].Approach;
        return new M10ApproachDecision(
            winner.Attitude,
            ranked.Select(x => new M10ApproachRanking(
                x.Approach.Attitude,
                x.Approach.Prediction.WinProbability,
                x.Approach.StructuralScore,
                x.Approach.TacticalCandidate.TacticalScore,
                x.CompositeScore)).ToList());
    }

    private static bool IsValid(M10CandidateEvaluation x)
        => x.TacticalCandidate is not null && x.Prediction is not null && double.IsFinite(x.TacticalCandidate.TacticalScore);

    private static bool IsValidApproach(M10ApproachEvaluation x)
        => (x.Attitude is TeamAttitude.Normal or TeamAttitude.PlayItCool or TeamAttitude.MatchOfTheSeason) &&
           x.TacticalCandidate is not null && x.Prediction is not null && double.IsFinite(x.TacticalCandidate.TacticalScore);

    private static double CompositeScore(
        double tactical,
        double winProbability,
        double structural,
        double tacticalWeight,
        double predictionWeight,
        double structuralWeight)
    {
        if (tacticalWeight < 0 || predictionWeight < 0 || structuralWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(tacticalWeight));
        var total = tacticalWeight + predictionWeight + structuralWeight;
        if (total <= 0) throw new ArgumentException("M10 ağırlıklarının toplamı sıfırdan büyük olmalıdır.");
        var tacticalNormalized = 1.0 / (1.0 + Math.Exp(-Math.Clamp(tactical, -20.0, 20.0)));
        return ((tacticalWeight * tacticalNormalized) +
                (predictionWeight * Math.Clamp(winProbability, 0.0, 1.0)) +
                (structuralWeight * Math.Clamp(structural, 0.0, 1.0))) / total;
    }

    private static int ApproachOrder(TeamAttitude attitude) => attitude switch
    {
        TeamAttitude.Normal => 0,
        TeamAttitude.PlayItCool => 1,
        TeamAttitude.MatchOfTheSeason => 2,
        _ => 9
    };

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));

    private sealed record RankedCandidate(M10CandidateEvaluation Candidate, double CompositeScore);
    private sealed record RankedApproach(M10ApproachEvaluation Approach, double CompositeScore);
}

public sealed record M10CandidateEvaluation(
    TacticalCandidate TacticalCandidate,
    MatchPrediction Prediction,
    double StructuralScore);

public sealed record M10ApproachEvaluation(
    TeamAttitude Attitude,
    TacticalCandidate TacticalCandidate,
    MatchPrediction Prediction,
    double StructuralScore);

public sealed record M10RankedCandidate(
    string Formation,
    string CandidateId,
    double TacticalScore,
    double WinProbability,
    double CompositeScore);

public sealed record M10FormationCompetition(
    string Formation,
    string BestCandidateId,
    double TacticalScore,
    double WinProbability,
    double CompositeScore,
    int CandidateCount)
{
    public int Rank { get; init; }
    public double MarginVsNext { get; init; }
    public M10SearchDepthStatus SearchDepthStatus { get; init; }
}

public enum M10SearchDepthStatus
{
    Insufficient,
    Sufficient
}

public sealed record M10ApproachRanking(
    TeamAttitude Attitude,
    double WinProbability,
    double StructuralScore,
    double TacticalScore,
    double CompositeScore);

public sealed record M10ApproachDecision(
    TeamAttitude SelectedApproach,
    IReadOnlyList<M10ApproachRanking> Ranking);

public sealed record M10DecisionResult(
    FinalMatchPlan BestPlan,
    MatchPrediction Prediction,
    IReadOnlyList<M10RankedCandidate> Ranking,
    M10DecisionStatus Status)
{
    public TeamAttitude? SelectedApproach { get; init; }
    public IReadOnlyList<M10ApproachRanking>? ApproachRanking { get; init; }
    public IReadOnlyList<M10FormationCompetition>? FormationCompetition { get; init; }
}

public enum M10DecisionStatus
{
    SelectedDeterministically,
    CalibrationRequired
}
