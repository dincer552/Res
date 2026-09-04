using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class DB1FormationCoverageRegression
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
            Console.WriteLine("=== C9 DB1 FORMATION COVERAGE REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context,players,cancellationToken,"offline-c9-db1");
            var legal = result.M4.Candidates.Select(x=>x.Formation).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList();
            Check(result.CandidateDatabase1Count > 0,"DB1 is not empty");
            Check(result.CandidateDatabase1Count >= legal.Count,"DB1 has at least one candidate per legal formation");
            Check(result.M6.EvaluatedCandidates >= legal.Count,"M6-A evaluated enough candidates to support legal formation coverage");
            var log = MotorRunLogStore.Get("offline-c9-db1");
            Check(log is not null,"DB1 run telemetry exists");
            Check(log!.Stages.Any(x=>x.Motor=="M6" && x.Status=="completed"),"M6 completed telemetry exists");
            Check(log.Stages.Any(x=>x.Motor=="M9" && x.Status=="completed"),"downstream M9 completed before DB1");
            Console.WriteLine($"DB1 count={result.CandidateDatabase1Count} | legal formations={legal.Count} | M6-A evaluated={result.M6.EvaluatedCandidates}");
            Console.WriteLine("PASS: C9 DB1 formation coverage continuity"); return 0;
        } catch(Exception ex){return Fail("C9 exception: "+ex.Message);}
    }
    private static Player ReadPlayer(JsonElement e)=>new(e.GetProperty("id").GetInt32(),e.GetProperty("name").GetString()??"Player",e.GetProperty("keeper").GetInt32(),e.GetProperty("defending").GetInt32(),e.GetProperty("playmaking").GetInt32(),e.GetProperty("passing").GetInt32(),e.GetProperty("winger").GetInt32(),e.GetProperty("scoring").GetInt32(),e.GetProperty("stamina").GetInt32(),e.GetProperty("form").GetInt32(),e.GetProperty("experience").GetInt32(),GetInt(e,"loyalty",0),GetInt(e,"injuryLevel",-1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e){var ld=GetDouble(e,"leftDefence");var cd=GetDouble(e,"centralDefence");var rd=GetDouble(e,"rightDefence");var mid=GetDouble(e,"midfield");var la=GetDouble(e,"leftAttack");var ca=GetDouble(e,"centralAttack");var ra=GetDouble(e,"rightAttack");return new RegionalRatingSnapshot(ld,cd,rd,mid,la,ca,ra,ld,cd,rd,mid,la,ca,ra);}
    private static string GetString(JsonElement e,string n,string f)=>e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??f:f; private static int GetInt(JsonElement e,string n,int f)=>e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var x)?x:f; private static double GetDouble(JsonElement e,string n)=>e.GetProperty(n).GetDouble(); private static void Check(bool ok,string m){if(!ok)throw new InvalidOperationException(m);} private static int Fail(string m){Console.WriteLine("FAIL: "+m);return 1;}
}
