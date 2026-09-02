using System.Collections.Concurrent;

namespace HattrickAI.V5.Core;

/// <summary>
/// Live M3 -> M10 orchestration. The UI receives only the final M10 plan as
/// the recommended XI; earlier motors are no longer allowed to bypass the
/// decision chain.
/// </summary>
public sealed class MotorPipelineService
{
    private readonly PlayerAnalysisEngine _m3 = new();
    private readonly FormationCandidateEngine _m4 = new();
    private readonly PositionOptimizationEngine _m5 = new();
    private readonly M6GlobalOptimizationEngine _m6 = new();
    private readonly RegionalRatingScenarioEngine _m7 = new();
    private readonly AdvancedTacticalScenarioEngine _m72 = new();
    private readonly M8ChanceModel _m8 = new();
    private readonly M9MatchPredictionEngine _m9 = new();
    private readonly M10FinalDecisionEngine _m10 = new();

    public async Task<MotorPipelineResult> RunAsync(
        MatchDataContext context,
        IReadOnlyList<Player> players,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);

        var m3 = _m3.Analyze(players);
        var m4 = _m4.Generate(context, m3);
        if (m4.Candidates.Count == 0)
            throw new InvalidOperationException("M4 geçerli bir diziliş adayı üretemedi.");

        // Every legal formation is evaluated by M5. The live request keeps the
        // strongest six XI candidates per formation so M6 remains bounded.
        var m5 = _m5.GenerateCandidates(context, m3, m4, maxCandidatesPerFormation: 6);
        if (m5.Count == 0)
            throw new InvalidOperationException("M5 geçerli bir XI adayı üretemedi.");

        var cache = new ConcurrentDictionary<string, CandidateEvaluation>(StringComparer.Ordinal);
        var m6 = await _m6.OptimizeAsync(
            m5,
            players,
            async (lineup, token) =>
            {
                token.ThrowIfCancellationRequested();
                var evaluation = Evaluate(lineup);
                cache[Signature(lineup)] = evaluation;
                await Task.CompletedTask;
                return evaluation.Tactical;
            },
            beamWidth: 6,
            maxIterations: 4,
            ct);

        if (m6.BestCandidate is null)
            throw new InvalidOperationException("M6 geçerli bir final davranış adayı üretemedi.");

        var bestKey = Signature(m6.BestCandidate.Lineup);
        if (!cache.TryGetValue(bestKey, out var bestEvaluation))
        {
            bestEvaluation = Evaluate(m6.BestCandidate.Lineup);
            cache[bestKey] = bestEvaluation;
        }

        var m9 = _m9.Predict(m6.BestCandidate, bestEvaluation.Chance, context.RatingContext.MatchLocation);
        var m10 = _m10.Select([
            new M10CandidateEvaluation(
                m6.BestCandidate,
                m9.Prediction,
                bestEvaluation.Chance.StructuralChanceIndex)
        ]);

        return new MotorPipelineResult(
            m3,
            m4,
            m5,
            m6,
            bestEvaluation.Scenario,
            bestEvaluation.Advanced,
            bestEvaluation.Chance,
            m9,
            m10,
            m10.BestPlan,
            m10.Prediction);

        CandidateEvaluation Evaluate(Lineup lineup)
        {
            var signature = Signature(lineup);
            var state = new MatchState(
                signature,
                lineup.Formation,
                signature,
                signature,
                context.RatingContext.MatchLocation,
                context.RatingContext.Attitude,
                TeamTactic.Normal,
                TeamSpiritValue(context.Questionnaire.TeamSpirit),
                context.Questionnaire.Coach);

            var scenario = _m7.CalculateLineup(lineup, players, state);
            var opponentAverage = Average(context.Opponent.Rating);
            var advanced = _m72.CalculateLineup(lineup, players, state, opponentAverage);
            var input = AdvancedTacticalScenarioEngine.BuildM8Input(scenario, advanced);
            var chance = _m8.Calculate(input, context.Opponent.Rating);
            var matchup = BuildMatchup(scenario.Rating, context.Opponent.Rating, chance);
            var tacticalScore = (0.70 * chance.StructuralChanceIndex) + (0.30 * matchup.OverallScore);
            var tactical = new TacticalCandidate(lineup, scenario.Rating, matchup, tacticalScore);
            return new CandidateEvaluation(tactical, scenario, advanced, chance);
        }
    }

    private static MatchupEvaluation BuildMatchup(RegionalRatingSnapshot own, RegionalRatingSnapshot opponent, M8ChanceResult chance)
    {
        static double signed(double share) => (Math.Clamp(share, 0, 1) * 2.0) - 1.0;
        var midfield = signed(chance.MidfieldShare);
        var left = signed(chance.LeftAttackVsRightDefence);
        var centre = signed(chance.CentreAttackVsCentreDefence);
        var right = signed(chance.RightAttackVsLeftDefence);
        var leftDef = signed(Share(own.LeftDefence, opponent.RightAttack));
        var centreDef = signed(Share(own.CentralDefence, opponent.CentralAttack));
        var rightDef = signed(Share(own.RightDefence, opponent.LeftAttack));
        var overall = (midfield + left + centre + right + leftDef + centreDef + rightDef) / 7.0;
        return new MatchupEvaluation(midfield, left, centre, right, leftDef, centreDef, rightDef, overall);
    }

    private static double Share(double own, double opponent)
    {
        var total = Math.Max(0, own) + Math.Max(0, opponent);
        return total <= 0 ? 0.5 : Math.Clamp(Math.Max(0, own) / total, 0, 1);
    }

    private static double Average(RegionalRatingSnapshot r)
        => (r.LeftDefence + r.CentralDefence + r.RightDefence + r.Midfield + r.LeftAttack + r.CentralAttack + r.RightAttack) / 7.0;

    private static double TeamSpiritValue(TeamSpiritLevel level) => level switch
    {
        TeamSpiritLevel.Murderous => 1, TeamSpiritLevel.Furious => 2, TeamSpiritLevel.Irritated => 3,
        TeamSpiritLevel.Composed => 4.5, TeamSpiritLevel.Calm => 5, TeamSpiritLevel.Content => 6,
        TeamSpiritLevel.Satisfied => 7, TeamSpiritLevel.Delirious => 8, TeamSpiritLevel.WalkingOnClouds => 9,
        TeamSpiritLevel.ParadiseOnEarth => 10, _ => 4.5
    };

    private static string Signature(Lineup lineup)
        => string.Join(";", lineup.Slots
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => s.PlayerId)
            .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));

    private sealed record CandidateEvaluation(
        TacticalCandidate Tactical,
        RatingScenarioResult Scenario,
        AdvancedTacticalScenarioResult Advanced,
        M8ChanceResult Chance);
}

public sealed record MotorPipelineResult(
    PlayerAnalysisResult M3,
    FormationCandidateSet M4,
    IReadOnlyList<PositionAssignmentCandidate> M5,
    M6OptimizationResult M6,
    RatingScenarioResult M7,
    AdvancedTacticalScenarioResult M72,
    M8ChanceResult M8,
    M9PredictionResult M9,
    M10DecisionResult M10,
    FinalMatchPlan FinalPlan,
    MatchPrediction FinalPrediction);
