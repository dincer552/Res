using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M6ACandidateEvaluationRegression
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");
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

            Console.WriteLine("=== C4 M6-A CANDIDATE EVALUATION REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c4-m6a");
            var legal = result.M4.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal).ToList();

            Check(result.M5.Count > 0, "M6-A receives non-empty M5 XI pool");
            Check(result.M6.EvaluatedCandidates > 0, "M6-A evaluates candidates");
            Check(result.M6.EvaluatedCandidates >= legal.Count, "M6-A evaluates at least one candidate per legal formation");
            Check(result.M6.BestCandidate is not null, "M6-A produces a best candidate");
            Check(result.M6.TopCandidates.Count > 0, "M6-A produces a ranked candidate set");
            Check(result.CandidateDatabase1Count > 0, "M6-A populates Candidate DB #1");
            Check(result.CandidateDatabase1Count >= legal.Count, "Candidate DB #1 retains at least one candidate per legal formation");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Count == 11), "every M6-A retained candidate has 11 slots");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Select(s => s.PlayerId).Distinct().Count() == 11), "every M6-A retained candidate uses 11 unique players");
            Check(result.M6.TopCandidates.All(x => x.Lineup.Slots.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count() == 11), "every M6-A retained candidate uses 11 unique slot codes");
            Check(result.M6.TopCandidates.All(x => double.IsFinite(x.TacticalScore)), "M6-A tactical scores are finite");
            Check(result.M6.TopCandidates.All(x => x.TacticalScore >= 0), "M6-A tactical scores are non-negative");
            Check(result.M6.TopCandidates.Select(x => x.Lineup.Formation).Distinct(StringComparer.Ordinal).Count() == legal.Count, "M6-A retained pool covers every legal formation");
            Check(result.M6.TopCandidates.Select(Signature).Distinct(StringComparer.Ordinal).Count() == result.M6.TopCandidates.Count, "M6-A retained candidates are unique");
            Check(result.M6.BestCandidate is null || double.IsFinite(result.M6.BestCandidate.TacticalScore), "M6-A best candidate score is finite");
            Check(result.M6.BestCandidate is null || result.M6.TopCandidates.Any(x => Signature(x.Lineup) == Signature(result.M6.BestCandidate.Lineup)), "M6-A best candidate is represented in retained pool");
            Check(result.M6.Converged || result.M6.Iterations > 0, "M6-A search reports a meaningful iteration state");

            Console.WriteLine($"M5 XI={result.M5.Count} | M6-A evaluated={result.M6.EvaluatedCandidates} | DB1={result.CandidateDatabase1Count} | retained={result.M6.TopCandidates.Count}");
            foreach (var group in result.M6.TopCandidates.GroupBy(x => x.Lineup.Formation).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var best = group.Max(x => x.TacticalScore);
                Console.WriteLine($"  {group.Key}: {group.Count()} retained candidates | best tactical={best:F4}");
            }
            Console.WriteLine("PASS: C4 M6-A candidate evaluation contract");
            Console.WriteLine("NEXT: C5 M7 regional rating gerçekten çağrılıyor");
            return 0;
        }
        catch (Exception ex) { return Fail("C4 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e) => new(GetDouble(e, "leftDefence"), GetDouble(e, "centralDefence"), GetDouble(e, "rightDefence"), GetDouble(e, "midfield"), GetDouble(e, "leftAttack"), GetDouble(e, "centralAttack"), GetDouble(e, "rightAttack"));
    private static string Signature(TacticalCandidate x) => string.Join(";", x.Lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private static string GetString(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
