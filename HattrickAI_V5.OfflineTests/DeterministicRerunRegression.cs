using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class DeterministicRerunRegression
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
            var opponent = new OpponentMatchProfile(
                GetString(analysis, "opponentName", "Opponent"),
                GetString(analysis, "opponentFormation", ""),
                opponentRating,
                new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(
                players,
                0,
                GetString(analysis.GetProperty("ownLineup"), "teamName", "Fixture"),
                opponent,
                RatingContext.Default,
                MatchQuestionnaire.Default);

            Console.WriteLine("=== C18 DETERMINISTIC RERUN REGRESSION ===");
            var pipeline = new MotorPipelineService();
            var result1 = await pipeline.RunAsync(context, players, cancellationToken, "offline-c18-run-1");
            var result2 = await pipeline.RunAsync(context, players, cancellationToken, "offline-c18-run-2");

            var fingerprint1 = Fingerprint(result1);
            var fingerprint2 = Fingerprint(result2);

            Check(fingerprint1 == fingerprint2, "two identical pipeline runs produced different fingerprints");
            Check(result1.M4.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal).SequenceEqual(
                result2.M4.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal), StringComparer.Ordinal),
                "M4 formation order changed between reruns");
            Check(result1.CandidateDatabase1Count == result2.CandidateDatabase1Count, "DB1 count changed between reruns");
            Check(result1.CandidateDatabase2Count == result2.CandidateDatabase2Count, "DB2 count changed between reruns");
            Check(result1.M11?.CandidateCount == result2.M11?.CandidateCount, "M11 finalist count changed between reruns");
            Check(result1.FinalPlan.Formation == result2.FinalPlan.Formation, "FinalPlan formation changed between reruns");
            Check(Signature(result1.FinalPlan.Lineup) == Signature(result2.FinalPlan.Lineup), "FinalPlan XI changed between reruns");
            Check(result1.FinalPrediction == result2.FinalPrediction, "FinalPrediction changed between reruns");
            Check(result1.M9.Prediction == result2.M9.Prediction, "selected M9 prediction changed between reruns");
            Check(result1.M9.Simulation.MostLikelyScore == result2.M9.Simulation.MostLikelyScore, "M9 most-likely score changed between reruns");
            Check(result1.M9.Simulation.Outcome == result2.M9.Simulation.Outcome, "M9 simulation outcome changed between reruns");

            Console.WriteLine($"Fingerprint={fingerprint1}");
            Console.WriteLine($"Run1={result1.FinalPlan.Formation} | Run2={result2.FinalPlan.Formation} | DB1={result1.CandidateDatabase1Count}/{result2.CandidateDatabase1Count} | DB2={result1.CandidateDatabase2Count}/{result2.CandidateDatabase2Count}");
            Console.WriteLine("PASS: C18 deterministic rerun");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("C18 exception: " + ex.Message);
        }
    }

    private static string Fingerprint(MotorPipelineResult result)
    {
        var payload = new
        {
            M4 = result.M4.Candidates.Select(x => new { x.Formation, x.Slots }).ToList(),
            M5 = result.M5.Select(x => new { x.CandidateId, x.Formation, x.StructuralScore, Slots = x.Lineup.Slots.Select(s => new { s.Code, s.PlayerId, s.Order }).ToList() }).ToList(),
            DB1 = result.CandidateDatabase1.Select(x => new { x.CandidateId, x.Formation, x.Stage, x.RankingScore }).ToList(),
            M10 = result.M10.FormationCompetition?.Select(x => new { x.Formation, x.Rank, x.CandidateCount, x.CompositeScore, x.SearchDepthStatus }).ToList(),
            DB2 = result.CandidateDatabase2.Select(x => new { x.CandidateId, x.Formation, x.Stage, x.RankingScore }).ToList(),
            M11 = result.M11 is null ? null : new
            {
                result.M11.CandidateCount,
                result.M11.FormationCount,
                Ranking = result.M11.Ranking.Select(x => new { x.CandidateId, x.Formation, x.FinalScore, x.TacticalScore, x.WinProbability }).ToList()
            },
            FinalPlan = new
            {
                result.FinalPlan.Formation,
                result.FinalPlan.TacticalScore,
                result.FinalPlan.Rating,
                result.FinalPlan.Matchup,
                Lineup = result.FinalPlan.Lineup.Slots.Select(s => new { s.Code, s.PlayerId, s.Order }).ToList()
            },
            FinalPrediction = result.FinalPrediction,
            M9 = new
            {
                result.M9.Prediction,
                result.M9.Simulation.MostLikelyScore,
                result.M9.Simulation.MostLikelyScoreProbability,
                result.M9.Simulation.Outcome,
                Scenarios = result.M9.Simulation.Scenarios.Select(x => new { x.Scenario, x.Count, x.MostLikelyScore }).ToList()
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static Player ReadPlayer(JsonElement e) => new(
        e.GetProperty("id").GetInt32(),
        e.GetProperty("name").GetString() ?? "Player",
        e.GetProperty("keeper").GetInt32(),
        e.GetProperty("defending").GetInt32(),
        e.GetProperty("playmaking").GetInt32(),
        e.GetProperty("passing").GetInt32(),
        e.GetProperty("winger").GetInt32(),
        e.GetProperty("scoring").GetInt32(),
        e.GetProperty("stamina").GetInt32(),
        e.GetProperty("form").GetInt32(),
        e.GetProperty("experience").GetInt32(),
        GetInt(e, "loyalty", 0),
        GetInt(e, "injuryLevel", -1));

    private static RegionalRatingSnapshot ReadRating(JsonElement e)
    {
        var ld = GetDouble(e, "leftDefence");
        var cd = GetDouble(e, "centralDefence");
        var rd = GetDouble(e, "rightDefence");
        var mid = GetDouble(e, "midfield");
        var la = GetDouble(e, "leftAttack");
        var ca = GetDouble(e, "centralAttack");
        var ra = GetDouble(e, "rightAttack");
        return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra);
    }

    private static string Signature(Lineup lineup) => string.Join(";", lineup.Slots
        .OrderBy(s => s.Code, StringComparer.Ordinal)
        .ThenBy(s => s.PlayerId)
        .Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));

    private static string GetString(JsonElement e, string n, string f) =>
        e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? f : f;

    private static int GetInt(JsonElement e, string n, int f) =>
        e.TryGetProperty(n, out var v) && v.TryGetInt32(out var x) ? x : f;

    private static double GetDouble(JsonElement e, string n) => e.GetProperty(n).GetDouble();

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
