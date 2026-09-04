using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M6BRefinementRegression
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement; var normalized = root.GetProperty("normalized"); var analysis = root.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var opponentRating = ReadRating(analysis.GetProperty("opponentRating"));
            var opponent = new OpponentMatchProfile(GetString(analysis,"opponentName","Opponent"),GetString(analysis,"opponentFormation",""),opponentRating,new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(players,0,GetString(analysis.GetProperty("ownLineup"),"teamName","Fixture"),opponent,RatingContext.Default,MatchQuestionnaire.Default);

            Console.WriteLine("=== C12 M6-B REFINEMENT REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c12-m6b");
            Check(result.M6B is not null, "M6-B result missing");
            Check(result.CandidateDatabase2Count > 0, "DB2 is empty after M6-B");
            Check(result.M6B!.EvaluatedCandidates > 0, "M6-B evaluated zero candidates");
            Check(result.M6B.TopCandidates.Count > 0, "M6-B retained no candidates");
            Check(result.M6B.BestCandidate is not null, "M6-B has no best candidate");
            Check(result.M6B.Iterations >= 1, "M6-B performed no search iteration");
            Check(result.M6B.EvaluatedCandidates >= result.M6B.TopCandidates.Count, "M6-B evaluated fewer candidates than retained");
            Check(result.M6B.TopCandidates.All(x => x.Lineup.Slots.Count == 11), "M6-B produced non-XI lineup");
            Check(result.M6B.TopCandidates.All(x => x.Lineup.Slots.Select(s=>s.PlayerId).Where(id=>id>0).Distinct().Count()==11), "M6-B produced duplicate XI players");
            Check(result.M6B.TopCandidates.All(x => double.IsFinite(x.TacticalScore)), "M6-B tactical score contains non-finite value");
            Check(result.M6B.TopCandidates.Select(x=>x.Lineup.Formation).Distinct(StringComparer.Ordinal).Count() == result.M4.Candidates.Select(x=>x.Formation).Distinct(StringComparer.Ordinal).Count(), "M6-B lost a legal formation during refinement");
            Check(result.M6B.BestCandidate.Lineup.Formation == result.M10.BestPlan.Formation || result.M6B.TopCandidates.Any(x=>x.Lineup.Formation == result.M10.BestPlan.Formation), "M10 winner formation did not reach M6-B");

            var log = MotorRunLogStore.Get("offline-c12-m6b");
            Check(log is not null, "M6-B telemetry missing");
            var stage = log!.Stages.FirstOrDefault(x=>x.Motor=="M6-B" && x.Status=="completed");
            Check(stage is not null, "M6-B completion telemetry missing");
            Check(stage!.CandidateCount.GetValueOrDefault() == result.M6B.EvaluatedCandidates, "M6-B telemetry evaluated count mismatch");
            Check(stage.Message?.Contains("M10 rank-driven", StringComparison.OrdinalIgnoreCase) == true, "M6-B telemetry does not identify rank-driven refinement");

            Console.WriteLine($"M6-B evaluated={result.M6B.EvaluatedCandidates} | retained={result.M6B.TopCandidates.Count} | iterations={result.M6B.Iterations} | DB2={result.CandidateDatabase2Count}");
            Console.WriteLine("PASS: C12 M6-B refinement"); return 0;
        }
        catch(Exception ex){ return Fail("C12 exception: "+ex.Message); }
    }
    private static Player ReadPlayer(JsonElement e)=>new(e.GetProperty("id").GetInt32(),e.GetProperty("name").GetString()??"Player",e.GetProperty("keeper").GetInt32(),e.GetProperty("defending").GetInt32(),e.GetProperty("playmaking").GetInt32(),e.GetProperty("passing").GetInt32(),e.GetProperty("winger").GetInt32(),e.GetProperty("scoring").GetInt32(),e.GetProperty("stamina").GetInt32(),e.GetProperty("form").GetInt32(),e.GetProperty("experience").GetInt32(),GetInt(e,"loyalty",0),GetInt(e,"injuryLevel",-1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e){var ld=GetDouble(e,"leftDefence");var cd=GetDouble(e,"centralDefence");var rd=GetDouble(e,"rightDefence");var mid=GetDouble(e,"midfield");var la=GetDouble(e,"leftAttack");var ca=GetDouble(e,"centralAttack");var ra=GetDouble(e,"rightAttack");return new RegionalRatingSnapshot(ld,cd,rd,mid,la,ca,ra,ld,cd,rd,mid,la,ca,ra);}
    private static string GetString(JsonElement e,string n,string f)=>e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??f:f; private static int GetInt(JsonElement e,string n,int f)=>e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var x)?x:f; private static double GetDouble(JsonElement e,string n)=>e.GetProperty(n).GetDouble(); private static void Check(bool ok,string m){if(!ok)throw new InvalidOperationException(m);} private static int Fail(string m){Console.WriteLine("FAIL: "+m);return 1;}
}
