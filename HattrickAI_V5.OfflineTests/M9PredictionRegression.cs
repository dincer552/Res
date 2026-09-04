using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M9PredictionRegression
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
            var lineup = analysis.GetProperty("ownLineup");
            var teamName = GetString(lineup, "teamName", "Fixture");
            var context = new MatchDataContext(players, 0, teamName, opponent, RatingContext.Default, MatchQuestionnaire.Default);
            Console.WriteLine("=== C8 M9 PREDICTION REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c8-m9");
            var m9 = result.M9;
            Check(m9 is not null, "production pipeline returned M9 result");
            Check(m9.CandidateId == result.M6.BestCandidate?.Lineup is not null ? CandidateId(result.M6.BestCandidate.Lineup) : m9.CandidateId, "M9 candidate identity is consistent");
            var p = m9.Prediction;
            Check(double.IsFinite(p.ExpectedHomeGoals) && double.IsFinite(p.ExpectedAwayGoals), "M9 expected goals are finite");
            Check(p.ExpectedHomeGoals >= 0.05 && p.ExpectedHomeGoals <= 5.0, "M9 own expected goals are clamped to production bounds");
            Check(p.ExpectedAwayGoals >= 0.05 && p.ExpectedAwayGoals <= 5.0, "M9 opponent expected goals are clamped to production bounds");
            Check(double.IsFinite(p.WinProbability) && double.IsFinite(p.DrawProbability) && double.IsFinite(p.LossProbability), "M9 W/D/L are finite");
            Check(p.WinProbability >= 0 && p.WinProbability <= 1 && p.DrawProbability >= 0 && p.DrawProbability <= 1 && p.LossProbability >= 0 && p.LossProbability <= 1, "M9 W/D/L are bounded");
            Check(Math.Abs((p.WinProbability + p.DrawProbability + p.LossProbability) - 1.0) <= 1e-9, "M9 W/D/L sum to 1");
            Check(m9.EventGoals is not null && m9.OpponentEventGoals is not null, "M9 event layers are present");
            var simulation = m9.Simulation;
            Check(simulation.Outcome is not null, "M9 Monte Carlo outcome exists");
            Check(Math.Abs(simulation.Outcome.WinProbability + simulation.Outcome.DrawProbability + simulation.Outcome.LossProbability - 1.0) <= 1e-9, "M9 simulation W/D/L sum to 1");
            Check(!string.IsNullOrWhiteSpace(m9.MostLikelyScore), "M9 most-likely score exists");
            var direct = new M9MatchPredictionEngine().Predict(
                result.M6.BestCandidate!, result.M8, opponentRating, context.RatingContext.MatchLocation,
                players, opponent.LastMatchLineup, opponent.Players);
            Check(Equal(direct.Prediction.ExpectedHomeGoals, p.ExpectedHomeGoals), "M9 direct recalculation matches pipeline expected home goals");
            Check(Equal(direct.Prediction.ExpectedAwayGoals, p.ExpectedAwayGoals), "M9 direct recalculation matches pipeline expected away goals");
            Check(Equal(direct.Prediction.WinProbability, p.WinProbability), "M9 direct recalculation matches pipeline win probability");
            Check(Equal(direct.Prediction.DrawProbability, p.DrawProbability), "M9 direct recalculation matches pipeline draw probability");
            Check(Equal(direct.Prediction.LossProbability, p.LossProbability), "M9 direct recalculation matches pipeline loss probability");
            Console.WriteLine($"M9 formation={m9.Formation} | W/D/L={p.WinProbability:P1}/{p.DrawProbability:P1}/{p.LossProbability:P1} | xG={p.ExpectedHomeGoals:0.###}-{p.ExpectedAwayGoals:0.###} | score={m9.MostLikelyScore}");
            Console.WriteLine("PASS: C8 M9 prediction continuity");
            return 0;
        }
        catch (Exception ex) { return Fail("C8 exception: " + ex.Message); }
    }
    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e) { var ld=GetDouble(e,"leftDefence"); var cd=GetDouble(e,"centralDefence"); var rd=GetDouble(e,"rightDefence"); var mid=GetDouble(e,"midfield"); var la=GetDouble(e,"leftAttack"); var ca=GetDouble(e,"centralAttack"); var ra=GetDouble(e,"rightAttack"); return new RegionalRatingSnapshot(ld,cd,rd,mid,la,ca,ra,ld,cd,rd,mid,la,ca,ra); }
    private static string GetString(JsonElement e,string n,string f)=>e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??f:f;
    private static int GetInt(JsonElement e,string n,int f)=>e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var x)?x:f;
    private static double GetDouble(JsonElement e,string n)=>e.GetProperty(n).GetDouble();
    private static string CandidateId(Lineup l)=>string.Join(";",l.Slots.OrderBy(s=>s.Code,StringComparer.Ordinal).ThenBy(s=>s.PlayerId).Select(s=>$"{s.Code}:{s.PlayerId}:{(int)s.Order}"));
    private static bool Equal(double a,double b)=>Math.Abs(a-b)<=1e-9;
    private static void Check(bool ok,string m){if(!ok)throw new InvalidOperationException(m);}
    private static int Fail(string m){Console.WriteLine("FAIL: "+m);return 1;}
}
