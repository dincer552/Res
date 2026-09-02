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
    private readonly M11FinalSelectorEngine _m11 = new();

    public async Task<MotorPipelineResult> RunAsync(MatchDataContext context, IReadOnlyList<Player> players, CancellationToken ct, string? runId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);
        runId ??= MotorRunLogContext.CurrentRunId;
        var cache = new ConcurrentDictionary<string, CandidateEvaluation>(StringComparer.Ordinal);
        var databases = new CandidateDatabaseSet();

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
            LogStart(runId, "M5", "Geniş XI havuzu: yaklaşık 20 / formasyon");
            var m5 = _m5.GenerateCandidates(context, m3, m4, maxCandidatesPerFormation: 20);
            if (m5.Count == 0) throw new InvalidOperationException("M5 geçerli bir XI adayı üretemedi.");
            LogComplete(runId, "M5", $"{m5.Count} XI adayı üretildi", sw.ElapsedMilliseconds, m5.Count);

            sw.Restart();
            LogStart(runId, "M6", "M6-A: geniş global search + Candidate DB #1");
            M6OptimizationResult m6;
            try
            {
                m6 = await _m6.OptimizeAsync(
                    m5,
                    players,
                    async (lineup, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var evaluation = Evaluate(lineup, runId, context.RatingContext.Attitude, logStages: false);
                        cache[Signature(lineup)] = evaluation;
                        databases.FirstPass.Add(new CandidateEvaluationRecord(
                            Signature(lineup), lineup.Formation, lineup, 0, 0,
                            evaluation.Tactical.TacticalScore, evaluation.Scenario.Rating,
                            evaluation.Advanced, evaluation.Chance, null,
                            evaluation.Tactical.TacticalScore, "M6-A"));
                        await Task.CompletedTask;
                        return evaluation.Tactical;
                    },
                    beamWidth: 6,
                    maxIterations: 4,
                    ct,
                    progress: (iteration, maximum, evaluated, retained) =>
                    {
                        if (!string.IsNullOrWhiteSpace(runId))
                            MotorRunLogStore.Progress(runId, "M6", $"A {iteration}/{maximum} • {evaluated} değerlendirildi • DB1 {databases.FirstPass.Count}", iteration, maximum);
                    });
            }
            catch (Exception ex)
            {
                LogFail(runId, "M6", ex.Message, sw.ElapsedMilliseconds);
                throw;
            }

            if (m6.BestCandidate is null || m6.TopCandidates.Count == 0)
                throw new InvalidOperationException("M6-A geçerli aday database'i oluşturamadı.");
            LogComplete(runId, "M6", $"M6-A • {m6.Iterations}/4 iteration • {m6.EvaluatedCandidates} değerlendirildi • DB1 {databases.FirstPass.Count} aday", sw.ElapsedMilliseconds, m6.EvaluatedCandidates);

            // DB #1 içindeki gerçek M6 adaylarını M10 review için M9 tahminiyle zenginleştir.
            var firstPassCandidates = m6.TopCandidates
                .Select(candidate =>
                {
                    var prediction = _m9.Predict(candidate, GetChance(candidate), context.RatingContext.MatchLocation);
                    return new M10CandidateEvaluation(candidate, prediction.Prediction, GetChance(candidate).StructuralChanceIndex);
                })
                .ToList();

            if (firstPassCandidates.Count == 0)
                throw new InvalidOperationException("Candidate DB #1 M10 değerlendirmesi için boş.");

            sw.Restart();
            LogStart(runId, "M10", $"DB1 review: {firstPassCandidates.Count} finalist adayı karşılaştırılıyor");
            var m10 = _m10.Select(firstPassCandidates);
            LogComplete(runId, "M10", $"DB1 review tamamlandı • lider {m10.BestPlan.Formation}", sw.ElapsedMilliseconds, firstPassCandidates.Count);

            // M10'un ilk review'ı ikinci M6 aramasının girişini oluşturur.
            // İlk 100'ün formation çeşitliliği korunur; ikinci tur aynı güçlü XI'lerin
            // legal Individual Order komşuluklarını yeniden tarar.
            var searchSeeds = databases.FirstPass.TopWithFormationDiversity(100, CandidateEvaluationDatabase.MaxPerFormation)
                .Select(record => ToPositionCandidate(record.Lineup, record.Formation, record.RankingScore))
                .ToList();

            sw.Restart();
            LogStart(runId, "M6-B", $"İkinci search • {searchSeeds.Count} seed");
            M6OptimizationResult m6b;
            try
            {
                m6b = await _m6.OptimizeAsync(
                    searchSeeds,
                    players,
                    async (lineup, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var evaluation = Evaluate(lineup, runId, context.RatingContext.Attitude, logStages: false);
                        cache["B:" + Signature(lineup)] = evaluation;
                        databases.SecondPass.Add(new CandidateEvaluationRecord(
                            Signature(lineup), lineup.Formation, lineup, 0, 0,
                            evaluation.Tactical.TacticalScore, evaluation.Scenario.Rating,
                            evaluation.Advanced, evaluation.Chance, null,
                            evaluation.Tactical.TacticalScore, "M6-B"));
                        await Task.CompletedTask;
                        return evaluation.Tactical;
                    },
                    beamWidth: 6,
                    maxIterations: 3,
                    ct,
                    progress: (iteration, maximum, evaluated, retained) =>
                    {
                        if (!string.IsNullOrWhiteSpace(runId))
                            MotorRunLogStore.Progress(runId, "M6-B", $"B {iteration}/{maximum} • {evaluated} değerlendirildi • DB2 {databases.SecondPass.Count}", iteration, maximum);
                    });
            }
            catch (Exception ex)
            {
                LogFail(runId, "M6-B", ex.Message, sw.ElapsedMilliseconds);
                throw;
            }

            if (m6b.TopCandidates.Count == 0)
                throw new InvalidOperationException("M6-B Candidate DB #2 oluşturamadı.");
            LogComplete(runId, "M6-B", $"İkinci search tamamlandı • DB2 {databases.SecondPass.Count} aday", sw.ElapsedMilliseconds, m6b.EvaluatedCandidates);

            // DB #2 artık M11'in gerçek final havuzudur.
            var finalists = databases.SecondPass.TopWithFormationDiversity(100, CandidateEvaluationDatabase.MaxPerFormation)
                .Select(record =>
                {
                    var tactical = record.TacticalScore;
                    var chance = record.Chance;
                    var candidate = record.Lineup;
                    var tacticalCandidate = record.CandidateId.StartsWith("", StringComparison.Ordinal)
                        ? m6b.TopCandidates.FirstOrDefault(x => Signature(x.Lineup) == record.CandidateId)
                        : null;
                    if (tacticalCandidate is null)
                    {
                        tacticalCandidate = m6b.TopCandidates.FirstOrDefault(x => Signature(x.Lineup) == Signature(candidate));
                    }
                    if (tacticalCandidate is null) return null;
                    var prediction = _m9.Predict(tacticalCandidate, chance, context.RatingContext.MatchLocation);
                    return new M11CandidateEvaluation(tacticalCandidate, prediction.Prediction, chance.StructuralChanceIndex, 1.0);
                })
                .Where(x => x is not null)
                .Cast<M11CandidateEvaluation>()
                .ToList();

            if (finalists.Count == 0)
                throw new InvalidOperationException("M11 final havuzu oluşturulamadı.");

            sw.Restart();
            LogStart(runId, "M11", $"DB2 final selection: {finalists.Count} aday");
            var m11 = _m11.Select(finalists);
            LogComplete(runId, "M11", $"FINAL: {m11.BestPlan.Formation} • {m11.CandidateCount} aday • {m11.FormationCount} formasyon", sw.ElapsedMilliseconds, m11.CandidateCount);

            var selectedKey = Signature(m11.BestPlan.Lineup);
            var selectedEvaluation = finalists.First(x => Signature(x.TacticalCandidate.Lineup) == selectedKey);
            var selectedChance = selectedEvaluation.TacticalCandidate.Lineup == m11.BestPlan.Lineup
                ? finalists.First(x => Signature(x.TacticalCandidate.Lineup) == selectedKey)
                : selectedEvaluation;
            var selectedM9 = _m9.Predict(selectedEvaluation.TacticalCandidate, selectedEvaluation.Prediction, context.RatingContext.MatchLocation);

            return new MotorPipelineResult(
                m3, m4, m5, m6,
                selectedEvaluation.TacticalCandidate.Lineup == m11.BestPlan.Lineup
                    ? GetScenario(selectedEvaluation.TacticalCandidate.Lineup, context.RatingContext.Attitude)
                    : GetScenario(selectedEvaluation.TacticalCandidate.Lineup, context.RatingContext.Attitude),
                GetAdvanced(selectedEvaluation.TacticalCandidate.Lineup, context.RatingContext.Attitude),
                selectedChance.TacticalCandidate is null ? throw new InvalidOperationException() : GetChance(selectedEvaluation.TacticalCandidate),
                selectedM9, m10, m11.BestPlan, m11.Prediction)
            {
                M11 = m11,
                CandidateDatabase1Count = databases.FirstPass.Count,
                CandidateDatabase2Count = databases.SecondPass.Count,
                SelectedMatchApproach = context.RatingContext.Attitude == TeamAttitude.Auto ? TeamAttitude.Normal : context.RatingContext.Attitude
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

        M8ChanceResult GetChance(TacticalCandidate candidate)
        {
            var key = Signature(candidate.Lineup);
            if (cache.TryGetValue(key, out var evaluation)) return evaluation.Chance;
            var evaluation2 = Evaluate(candidate.Lineup, runId, context.RatingContext.Attitude, false);
            cache[key] = evaluation2;
            return evaluation2.Chance;
        }

        RatingScenarioResult GetScenario(Lineup lineup, TeamAttitude attitude)
        {
            var key = Signature(lineup);
            if (cache.TryGetValue(key, out var evaluation)) return evaluation.Scenario;
            var result = Evaluate(lineup, runId, attitude, false);
            cache[key] = result;
            return result.Scenario;
        }

        AdvancedTacticalScenarioResult GetAdvanced(Lineup lineup, TeamAttitude attitude)
        {
            var key = Signature(lineup);
            if (cache.TryGetValue(key, out var evaluation)) return evaluation.Advanced;
            var result = Evaluate(lineup, runId, attitude, false);
            cache[key] = result;
            return result.Advanced;
        }
    }

    private static PositionAssignmentCandidate ToPositionCandidate(Lineup lineup, string formation, double rankingScore)
        => new(
            formation,
            lineup,
            Math.Max(0.001, rankingScore),
            lineup.Slots.ToDictionary(x => x.PlayerId, x => x.Code),
            1.0);

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
    public M11DecisionResult? M11 { get; init; }
    public int CandidateDatabase1Count { get; init; }
    public int CandidateDatabase2Count { get; init; }
    public TeamAttitude SelectedMatchApproach { get; init; } = TeamAttitude.Normal;
    public IReadOnlyList<M10ApproachRanking>? AutoApproachRanking { get; init; }
}
