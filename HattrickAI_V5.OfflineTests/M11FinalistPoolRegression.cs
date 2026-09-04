using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M11FinalistPoolRegression
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
            var opponent = new OpponentMatchProfile(GetString(analysis, "opponentName", "Opponent"), GetString(analysis, "opponentFormation", ""), opponentRating, new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(players, 0, GetString(analysis.GetProperty("ownLineup"), "teamName", "Fixture"), opponent, RatingContext.Default, MatchQuestionnaire.Default);

            Console.WriteLine("=== C14 M11 FINALIST POOL REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c14-m11-pool");
            var legal = result.M4.Candidates.Select(x => x.Formation).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            var db2 = result.CandidateDatabase2;

            Check(result.M11 is not null, "M11 result missing");
            Check(db2.Count > 0, "DB2 is empty");
            Check(result.M11!.CandidateCount > 0, "M11 finalist pool is empty");
            Check(result.M11.FormationCount == legal.Count, "M11 finalist pool lost a legal formation");
            Check(db2.Select(x => x.CandidateId).Distinct(StringComparer.Ordinal).Count() == db2.Count, "DB2 contains duplicate candidate IDs");
            Check(db2.All(x => x.Stage == "M6-B"), "M11 pool contains a non-M6-B DB2 record");
            Check(db2.All(x => x.Lineup.Slots.Count == 11), "M11 pool contains a non-XI lineup");
            Check(db2.All(x => x.Lineup.Slots.Select(s => s.PlayerId).Where(id => id > 0).Distinct().Count() == 11), "M11 pool contains duplicate XI players");
            Check(db2.All(x => x.Prediction is not null), "M11 pool contains candidate without M9 prediction");
            Check(db2.All(x => double.IsFinite(x.RankingScore) && double.IsFinite(x.TacticalScore) && double.IsFinite(x.Chance.StructuralChanceIndex)), "M11 pool contains non-finite score");
            Check(legal.All(f => db2.Any(x => x.Formation.Equals(f, StringComparison.Ordinal))), "DB2/M11 source pool does not cover every legal formation");

            var finalistFormations = db2.Select(x => x.Formation).Distinct(StringComparer.Ordinal).ToList();
            Check(finalistFormations.Count == legal.Count, "M11 finalist source contains unexpected formation count");
            Check(result.M11.BestPlan is not null, "M11 best plan missing");
            Check(legal.Contains(result.M11.BestPlan.Formation, StringComparer.Ordinal), "M11 best plan uses an illegal formation");
            Check(db2.Any(x => x.CandidateId == result.M11.BestPlan.Lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}").Aggregate((a, b) => a + ";" + b)), "M11 best plan is not sourced from DB2");

            var log = MotorRunLogStore.Get("offline-c14-m11-pool");
            Check(log is not null, "M11 telemetry missing");
            var m6b = log!.Stages.FirstOrDefault(x => x.Motor == "M6-B" && x.Status == "completed");
            var m11 = log.Stages.FirstOrDefault(x => x.Motor == "M11" && x.Status == "completed");
            Check(m6b is not null && m11 is not null, "M6-B/M11 completion telemetry missing");
            Check(log.Stages.IndexOf(m6b!) < log.Stages.IndexOf(m11!), "M11 completed before M6-B");
            Check(m11!.CandidateCount.GetValueOrDefault() == result.M11.CandidateCount, "M11 telemetry finalist count mismatch");
            Check(m11.Message?.Contains("DB2 final selection", StringComparison.OrdinalIgnoreCase) == true, "M11 telemetry does not identify DB2 final selection");

            Console.WriteLine($"M11 finalists={result.M11.CandidateCount} | formations={result.M11.FormationCount} | DB2={db2.Count}");
            Console.WriteLine("PASS: C14 M11 finalist pool");
            return 0;
        }
        catch (Exception ex) { return Fail("C14 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e) { var ld = GetDouble(e, "leftDefence"); var cd = GetDouble(e, "centralDefence"); var rd = GetDouble(e, "rightDefence"); var mid = GetDouble(e, "midfield"); var la = GetDouble(e, "leftAttack"); var ca = GetDouble(e, "centralAttack"); var ra = GetDouble(e, "rightAttack"); return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra); }
    private static string GetString(JsonElement e, string n, string f) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? f : f;
    private static int GetInt(JsonElement e, string n, int f) => e.TryGetProperty(n, out var v) && v.TryGetInt32(out var x) ? x : f;
    private static double GetDouble(JsonElement e, string n) => e.GetProperty(n).GetDouble();
    private static int Fail(string m) { Console.WriteLine("FAIL: " + m); return 1; }
    private static void Check(bool ok, string m) { if (!ok) throw new InvalidOperationException(m); }
}
