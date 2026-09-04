using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class FinalPlanContinuityRegression
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

            Console.WriteLine("=== C16 FINALPLAN CONTINUITY REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c16-finalplan");
            var m11 = result.M11;
            Check(m11 is not null, "M11 result missing");
            Check(result.FinalPlan is not null, "FinalPlan missing");
            Check(result.FinalPrediction is not null, "FinalPrediction missing at FinalPlan boundary");
            Check(result.FinalPlan.Formation == m11!.BestPlan.Formation, "FinalPlan formation differs from M11 BestPlan");
            Check(Signature(result.FinalPlan.Lineup) == Signature(m11.BestPlan.Lineup), "FinalPlan lineup differs from M11 BestPlan lineup");
            Check(Math.Abs(result.FinalPlan.TacticalScore - m11.BestPlan.TacticalScore) < 1e-12, "FinalPlan tactical score differs from M11 BestPlan");
            Check(result.FinalPlan.Rating == m11.BestPlan.Rating, "FinalPlan regional rating differs from M11 BestPlan");
            Check(result.FinalPlan.Matchup == m11.BestPlan.Matchup, "FinalPlan matchup differs from M11 BestPlan");
            Check(result.FinalPrediction == m11.Prediction, "FinalPrediction differs from M11 prediction");
            Check(result.FinalPlan.Lineup.Slots.Count == 11, "FinalPlan lineup is not an XI");
            Check(result.FinalPlan.Lineup.Slots.Select(x => x.PlayerId).Distinct().Count() == 11, "FinalPlan lineup contains duplicate players");
            Check(result.FinalPlan.Lineup.Slots.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == 11, "FinalPlan lineup contains duplicate slot codes");
            Check(double.IsFinite(result.FinalPlan.TacticalScore), "FinalPlan tactical score is not finite");

            Console.WriteLine($"FinalPlan={result.FinalPlan.Formation} | XI=11 | M11 winner continuity=OK");
            Console.WriteLine("PASS: C16 FinalPlan continuity");
            return 0;
        }
        catch (Exception ex) { return Fail("C16 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e) { var ld = GetDouble(e, "leftDefence"); var cd = GetDouble(e, "centralDefence"); var rd = GetDouble(e, "rightDefence"); var mid = GetDouble(e, "midfield"); var la = GetDouble(e, "leftAttack"); var ca = GetDouble(e, "centralAttack"); var ra = GetDouble(e, "rightAttack"); return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra); }
    private static string Signature(Lineup lineup) => string.Join(";", lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private static string GetString(JsonElement e, string n, string f) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? f : f;
    private static int GetInt(JsonElement e, string n, int f) => e.TryGetProperty(n, out var v) && v.TryGetInt32(out var x) ? x : f;
    private static double GetDouble(JsonElement e, string n) => e.GetProperty(n).GetDouble();
    private static int Fail(string m) { Console.WriteLine("FAIL: " + m); return 1; }
    private static void Check(bool ok, string m) { if (!ok) throw new InvalidOperationException(m); }
}
