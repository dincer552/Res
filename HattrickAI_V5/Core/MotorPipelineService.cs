using System.Collections.Concurrent;
using System.Diagnostics;

namespace HattrickAI.V5.Core;

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

    public async Task<MotorPipelineResult> RunAsync(MatchDataContext context, IReadOnlyList<Player> players, CancellationToken ct, string? runId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);
        runId ??= MotorRunLogContext.CurrentRunId;
        var cache = new ConcurrentDictionary<string, CandidateEvaluation>(StringComparer.Ordinal);

        var sw = Stopwatch.StartNew();
        try
        {
            LogStart(runId, "M3", "Çalışıyor");
            var m3 = _m3.Analyze(players);
            LogComplete(runId, "M3", $"{m3.Players.Count} oyuncu analiz edildi", sw.ElapsedMilliseconds, m3.Players.Count);

            sw.Restart();
            LogStart(runId, "M4", "Çalışıyor");
            var m4 = _m4.Generate(context, m3);
            if (m4.Candidates.Count == 0) throw new InvalidOperationException("M4 geçerli bir diziliş adayı üretemedi.");
            LogComplete(runId, "M4", $"{m4.Candidates.Count} diziliş üretildi", sw.ElapsedMilliseconds, m4.Candidates.Count);

            sw.Restart();
            LogStart(runId, "M5", "Çalışıyor");
            var m5 = _m5.GenerateCandidates(context, m3, m4, maxCandidatesPerFormation: 6);
            if (m5.Count == 0) throw new InvalidOperationException("M5 geçerli bir XI adayı üretemedi.");
            LogComplete(runId, "M5", $"{m5.Count} XI adayı üretildi", sw.ElapsedMilliseconds, m5.Count);

            sw.Restart();
            LogStart(runId, "M6", "1/4 iteration");
            M6OptimizationResult m6;
            try
            {
                m6 = await _m6.OptimizeAsync(
                    m5,
                    players,
                    async (lineup, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var evaluation = Evaluate(lineup, runId, context.RatingContext.Attitude, logStages: true);
                        cache[Signature(lineup)] = evaluation;
                        await Task.CompletedTask;
                        return evaluation.Tactical;
                    },
                    beamWidth: 6,
                    maxIterations: 4,
                    ct,
                    progress: (iteration, maximum, evaluated, retained) =>
                    {
                        if (!string.IsNullOrWhiteSpace(runId))
                            MotorRunLogStore.Progress(runId, "M6", $"{iteration}/{maximum} iteration • {evaluated} değerlendirildi", iteration, maximum);
                    });
            }
            catch (Exception ex)
            {
                LogFail(runId, "M6", ex.Message, sw.ElapsedMilliseconds);
                throw;
            }

            if (m6.BestCandidate is null) throw new InvalidOperationException("M6 geçerli bir final davranış adayı üretemedi.");
            LogComplete(runId, "M6", $"{m6.Iterations}/4 iteration • {m6.EvaluatedCandidates} değerlendirildi • {m6.RetainedCandidates} tutuldu", sw.ElapsedMilliseconds, m6.EvaluatedCandidates);

            var bestKey = Signature(m6.BestCandidate.Lineup);
            if (!cache.TryGetValue(bestKey, out var bestEvaluation))
            {
                bestEvaluation = Evaluate(m6.BestCandidate.Lineup, runId, context.RatingContext.Attitude, logStages: true);
                cache[bestKey] = bestEvaluation;
            }

            var selectedApproach = context.RatingContext.Attitude;
            M10ApproachDecision? autoApproach = null;
            var selectedEvaluation = bestEvaluation;

            if (context.RatingContext.Attitude == TeamAttitude.Auto)
            {
                sw.Restart();
                LogStart(runId, "M10", "Auto yaklaşım: Normal / PIC / MOTS karşılaştırılıyor");
                var approachEvaluations = new List<M10ApproachEvaluation>(3);
                foreach (var attitude in new[] { TeamAttitude.Normal, TeamAttitude.PlayItCool, TeamAttitude.MatchOfTheSeason })
                {
                    var evaluation = Evaluate(m6.BestCandidate.Lineup, runId, attitude, logStages: false);
                    var prediction = _m9.Predict(evaluation.Tactical, evaluation.Chance, context.RatingContext.MatchLocation);
                    approachEvaluations.Add(new M10ApproachEvaluation(
                        attitude,
                        evaluation.Tactical,
                        prediction.Prediction,
                        evaluation.Chance.StructuralChanceIndex));
                }

                autoApproach = _m10.SelectApproach(approachEvaluations);
                selectedApproach = autoApproach.SelectedApproach;
                selectedEvaluation = approachEvaluations.First(x => x.Attitude == selectedApproach) switch
                {
                    var chosen => new CandidateEvaluation(
                        chosen.TacticalCandidate,
                        Evaluate(m6.BestCandidate.Lineup, runId, selectedApproach, logStages: false).Scenario,
                        Evaluate(m6.BestCandidate.Lineup, runId, selectedApproach, logStages: false).Advanced,
                        Evaluate(m6.BestCandidate.Lineup, runId, selectedApproach, logStages: false).Chance)
                };

                var autoSummary = string.Join(" • ", autoApproach.Ranking.Select(x =>
                    $"{ApproachLabel(x.Attitude)} {x.CompositeScore:0.000}"));
                LogComplete(runId, "M10", $"Auto seçim: {ApproachLabel(selectedApproach)} • {autoSummary}", sw.ElapsedMilliseconds);
            }

            if (context.RatingContext.Attitude != TeamAttitude.Auto)
            {
                selectedEvaluation = bestEvaluation;
            }

            sw.Restart();
            LogStart(runId, "M9", "Çalışıyor");
            var m9 = _m9.Predict(selectedEvaluation.Tactical, selectedEvaluation.Chance, context.RatingContext.MatchLocation);
            LogComplete(runId, "M9", "Maç tahmini üretildi", sw.ElapsedMilliseconds);

            sw.Restart();
            LogStart(runId, "M10", "Çalışıyor");
            var m10 = _m10.Select([new M10CandidateEvaluation(selectedEvaluation.Tactical, m9.Prediction, selectedEvaluation.Chance.StructuralChanceIndex)]) with
            {
                SelectedApproach = selectedApproach == TeamAttitude.Auto ? TeamAttitude.Normal : selectedApproach,
                ApproachRanking = autoApproach?.Ranking
            };
            LogComplete(runId, "M10", $"Final karar: {m10.BestPlan.Formation} • Yaklaşım: {ApproachLabel(selectedApproach)}", sw.ElapsedMilliseconds);

            return new MotorPipelineResult(m3, m4, m5, m6, selectedEvaluation.Scenario, selectedEvaluation.Advanced, selectedEvaluation.Chance, m9, m10, m10.BestPlan, m10.Prediction)
            {
                SelectedMatchApproach = selectedApproach == TeamAttitude.Auto ? TeamAttitude.Normal : selectedApproach,
                AutoApproachRanking = autoApproach?.Ranking
            };
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(runId))
            {
                var log = MotorRunLogStore.Get(runId);
                var active = log?.Stages.FirstOrDefault(x => x.Status == "running");
                if (active is not null) MotorRunLogStore.FailMotor(runId, active.Motor, ex.Message, sw.ElapsedMilliseconds);
            }
            throw;
        }

        CandidateEvaluation Evaluate(Lineup lineup, string? currentRunId, TeamAttitude attitude, bool logStages)
        {
            var signature = Signature(lineup);
            var state = new MatchState(signature, lineup.Formation, signature, signature, context.RatingContext.MatchLocation, attitude, TeamTactic.Normal, TeamSpiritValue(context.Questionnaire.TeamSpirit), context.Questionnaire.Coach);
            var stageWatch = Stopwatch.StartNew();
            var stage = "M7";
            try
            {
                if (logStages) LogStart(currentRunId, "M7", "Çalışıyor");
                var scenario = _m7.CalculateLineup(lineup, players, state);
                if (logStages) LogComplete(currentRunId, "M7", "Bölgesel rating hesaplandı", stageWatch.ElapsedMilliseconds);

                stage = "M7.2";
                stageWatch.Restart();
                if (logStages) LogStart(currentRunId, "M7.2", "Çalışıyor");
                var opponentAverage = Average(context.Opponent.Rating);
                var advanced = _m72.CalculateLineup(lineup, players, state, opponentAverage);
                if (logStages) LogComplete(currentRunId, "M7.2", $"Taktik senaryo: {advanced.Tactic}", stageWatch.ElapsedMilliseconds);

                stage = "M8";
                stageWatch.Restart();
                if (logStages) LogStart(currentRunId, "M8", "Çalışıyor");
                var input = AdvancedTacticalScenarioEngine.BuildM8Input(scenario, advanced);
                var chance = _m8.Calculate(input, context.Opponent.Rating);
                if (logStages) LogComplete(currentRunId, "M8", $"Şans indeksi {chance.StructuralChanceIndex:0.###}", stageWatch.ElapsedMilliseconds);

                var matchup = BuildMatchup(scenario.Rating, context.Opponent.Rating, chance);
                var tacticalScore = (0.70 * chance.StructuralChanceIndex) + (0.30 * matchup.OverallScore);
                return new CandidateEvaluation(new TacticalCandidate(lineup, scenario.Rating, matchup, tacticalScore), scenario, advanced, chance);
            }
            catch (Exception ex)
            {
                if (logStages) LogFail(currentRunId, stage, ex.Message, stageWatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    private static void LogStart(string? runId, string motor, string message) { if (!string.IsNullOrWhiteSpace(runId)) MotorRunLogStore.StartMotor(runId, motor, message); }
    private static void LogComplete(string? runId, string motor, string message, long durationMs = 0, int? candidateCount = null) { if (!string.IsNullOrWhiteSpace(runId)) MotorRunLogStore.CompleteMotor(runId, motor, message, durationMs, candidateCount); }
    private static void LogFail(string? runId, string motor, string message, long durationMs = 0) { if (!string.IsNullOrWhiteSpace(runId)) MotorRunLogStore.FailMotor(runId, motor, message, durationMs); }

    private static string ApproachLabel(TeamAttitude attitude) => attitude switch
    {
        TeamAttitude.PlayItCool => "PIC",
        TeamAttitude.MatchOfTheSeason => "MOTS",
        _ => "Normal"
    };

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

    private static double Average(RegionalRatingSnapshot r) => (r.LeftDefence + r.CentralDefence + r.RightDefence + r.Midfield + r.LeftAttack + r.CentralAttack + r.RightAttack) / 7.0;

    private static double TeamSpiritValue(TeamSpiritLevel level) => level switch
    {
        TeamSpiritLevel.Murderous => 1, TeamSpiritLevel.Furious => 2, TeamSpiritLevel.Irritated => 3,
        TeamSpiritLevel.Composed => 4.5, TeamSpiritLevel.Calm => 5, TeamSpiritLevel.Content => 6,
        TeamSpiritLevel.Satisfied => 7, TeamSpiritLevel.Delirious => 8, TeamSpiritLevel.WalkingOnClouds => 9,
        TeamSpiritLevel.ParadiseOnEarth => 10, _ => 4.5
    };

    private static string Signature(Lineup lineup) => string.Join(";", lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private sealed record CandidateEvaluation(TacticalCandidate Tactical, RatingScenarioResult Scenario, AdvancedTacticalScenarioResult Advanced, M8ChanceResult Chance);
}

public sealed record MotorPipelineResult(PlayerAnalysisResult M3, FormationCandidateSet M4, IReadOnlyList<PositionAssignmentCandidate> M5, M6OptimizationResult M6, RatingScenarioResult M7, AdvancedTacticalScenarioResult M72, M8ChanceResult M8, M9PredictionResult M9, M10DecisionResult M10, FinalMatchPlan FinalPlan, MatchPrediction FinalPrediction)
{
    public TeamAttitude SelectedMatchApproach { get; init; } = TeamAttitude.Normal;
    public IReadOnlyList<M10ApproachRanking>? AutoApproachRanking { get; init; }
}
