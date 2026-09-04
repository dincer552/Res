using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M6ACandidateEvaluationRegression
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");
        var runId = MotorRunLogStore.Start("offline-acceptance-c4");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var normalized = root.GetProperty("normalized");
            var analysis = root.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var opponentRating = ReadRating(analysis.GetProperty("opponentRating"));
            var opponentName = GetString(analysis, "opponentName", "Opponent");
            var fixtureLineup = analysis.GetProperty("ownLineup");
            var teamName = GetString(fixtureLineup, "teamName", "Fixture");
            var opponentFormation = GetString(analysis, "opponentFormation", "");
            var opponent = new OpponentMatchProfile(opponentName, opponentFormation, opponentRating, new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(players, 0, teamName, opponent, RatingContext.Default, MatchQuestionnaire.Default);

            Console.WriteLine("=== C4 M6-A + EVALUATOR CHAIN REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, runId);
            var legal = result.M4.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal).ToList();
            var log = MotorRunLogStore.Get(runId) ?? throw new InvalidOperationException("M6-A run telemetry bulunamadı");

            Check(result.M5.Count > 0, "M6-A receives non-empty M5 XI pool");
            Check(result.M6.EvaluatedCandidates > 0, "M6-A evaluates candidates");
            Check(result.M6.BestCandidate is not null, "M6-A produces a best candidate");
            Check(result.M6.TopCandidates.Count > 0, "M6-A produces a ranked candidate set");
            Check(result.CandidateDatabase1Count >= legal.Count, "DB1 retains at least one candidate per legal formation");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Count == 11), "every M6-A retained candidate has 11 slots");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Select(s => s.PlayerId).Distinct().Count() == 11), "every M6-A retained candidate uses 11 unique players");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count() == 11), "every M6-A retained candidate uses 11 unique slot codes");
            Check(result.M6.TopCandidates.All(x => double.IsFinite(x.TacticalScore) && x.TacticalScore >= 0), "M6-A tactical scores are finite and non-negative");
            Check(result.M6.TopCandidates.Select(Signature).Distinct(StringComparer.Ordinal).Count() == result.M6.TopCandidates.Count, "M6-A retained candidates are unique");
            Check(result.M6.BestCandidate is null || result.M6.TopCandidates.Any(x => Signature(x.Lineup) == Signature(result.M6.BestCandidate.Lineup)), "M6-A best candidate is represented in retained pool");
            Check(result.M6.Converged || result.M6.Iterations > 0, "M6-A search reports a meaningful iteration state");

            // Run telemetry records the evaluated-candidate count for the downstream
            // chain. M6-A and M6-B share this run, so the count covers both passes.
            var m7 = log.Stages.Single(x => x.Motor == "M7");
            var m72 = log.Stages.Single(x => x.Motor == "M7.2");
            var m8 = log.Stages.Single(x => x.Motor == "M8");
            var m9 = log.Stages.Single(x => x.Motor == "M9");
            Check(m7.CandidateCount is > 0, "M7 telemetry has evaluated candidate count");
            Check(m72.CandidateCount is > 0, "M7.2 telemetry has evaluated candidate count");
            Check(m8.CandidateCount is > 0, "M8 telemetry has evaluated candidate count");
            Check(m9.CandidateCount is > 0, "M9 telemetry has evaluated candidate count");
            Check(m7.CandidateCount == m72.CandidateCount, "M7 and M7.2 evaluated candidate counts match");
            Check(m72.CandidateCount == m8.CandidateCount, "M7.2 and M8 evaluated candidate counts match");
            Check(m8.CandidateCount == m9.CandidateCount, "M8 and M9 evaluated candidate counts match");
            Check(m7.CandidateCount >= result.M6.EvaluatedCandidates, "evaluator chain count covers M6-A evaluations");
            Check(m7.Status == "completed" && m72.Status == "completed" && m8.Status == "completed" && m9.Status == "completed", "M7 → M7.2 → M8 → M9 stages completed");

            Console.WriteLine($"M5 XI={result.M5.Count} | M6-A evaluated={result.M6.EvaluatedCandidates} | DB1={result.CandidateDatabase1Count}");
            Console.WriteLine($"Real evaluator candidate telemetry: M7={m7.CandidateCount} | M7.2={m72.CandidateCount} | M8={m8.CandidateCount} | M9={m9.CandidateCount}");
            Console.WriteLine("PASS: C4 M6-A candidate evaluation + real M7 → M7.2 → M8 → M9 invocation chain");
            Console.WriteLine("NEXT: C5 M7 regional rating");
            return 0;
        }
        catch (Exception ex) { MotorRunLogStore.Finish(runId, false, ex.Message); return Fail("C4 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e)
    {
        var ld = GetDouble(e, "leftDefence"); var cd = GetDouble(e, "centralDefence"); var rd = GetDouble(e, "rightDefence"); var mid = GetDouble(e, "midfield"); var la = GetDouble(e, "leftAttack"); var ca = GetDouble(e, "centralAttack"); var ra = GetDouble(e, "rightAttack");
        return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra);
    }
    private static string Signature(TacticalCandidate x) => Signature(x.Lineup);
    private static string Signature(Lineup x) => string.Join(";", x.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private static string GetString(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
