using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M10ToM6BRankDrivenHandoffRegression
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");
        var runId = MotorRunLogStore.Start("offline-c11-handoff");
        try
        {
            await using var stream = File.OpenRead(path); using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root=doc.RootElement; var normalized=root.GetProperty("normalized"); var analysis=root.GetProperty("v5Analysis");
            var players=normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList(); var opponentRating=ReadRating(analysis.GetProperty("opponentRating"));
            var opponent=new OpponentMatchProfile(GetString(analysis,"opponentName","Opponent"),GetString(analysis,"opponentFormation",""),opponentRating,new OpponentThreatEngine().Analyze(opponentRating));
            var context=new MatchDataContext(players,0,GetString(analysis.GetProperty("ownLineup"),"teamName","Fixture"),opponent,RatingContext.Default,MatchQuestionnaire.Default);
            Console.WriteLine("=== C11 M10 -> M6-B RANK-DRIVEN HANDOFF REGRESSION ===");
            var result=await new MotorPipelineService().RunAsync(context,players,cancellationToken,runId);
            var competition=result.M10.FormationCompetition ?? []; var legal=result.M4.Candidates.Select(x=>x.Formation).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            Check(competition.Count==legal.Count,"M10 competition does not cover every legal formation"); Check(competition.Select(x=>x.Rank).OrderBy(x=>x).SequenceEqual(Enumerable.Range(1,competition.Count)),"M10 ranks are not contiguous");
            Check(result.M6BFormationBudgets.Count==competition.Count,"M6-B budget map depth differs from M10 competition depth"); Check(result.M6BFormationBudgets.Keys.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(competition.Select(x=>x.Formation).OrderBy(x=>x,StringComparer.Ordinal)),"M10 formations are not all handed off to M6-B");
            var expected=MotorPipelineService.BuildM6BFormationBudgetsForAcceptance(competition,6,3);
            foreach(var item in competition){Check(result.M6BFormationBudgets.TryGetValue(item.Formation,out var actual),$"missing M6-B budget for {item.Formation}");var expectedBudget=expected[item.Formation];Check(actual!.BeamWidth==expectedBudget.BeamWidth,$"M6-B beam width mismatch for {item.Formation}");Check(actual.MaxIterations==expectedBudget.MaxIterations,$"M6-B iteration budget mismatch for {item.Formation}");}
            var rankOrder=competition.OrderBy(x=>x.Rank).ToList(); for(var i=0;i<rankOrder.Count-1;i++){var a=result.M6BFormationBudgets[rankOrder[i].Formation];var b=result.M6BFormationBudgets[rankOrder[i+1].Formation];Check(a.BeamWidth>=b.BeamWidth,"better M10 rank received a weaker M6-B beam budget");Check(a.MaxIterations>=b.MaxIterations,"better M10 rank received a weaker M6-B iteration budget");}
            var log=MotorRunLogStore.Get(runId); Check(log is not null,"C11 run telemetry exists"); var m10=IndexOf(log!.Stages,x=>x.Motor=="M10"&&x.Status=="completed");var m6b=IndexOf(log.Stages,x=>x.Motor=="M6-B"&&x.Status=="completed");Check(m10>=0,"M10 completion telemetry missing");Check(m6b>=0,"M6-B completion telemetry missing");Check(m10<m6b,"M6-B completed before M10 handoff");
            MotorRunLogStore.Finish(runId,true,"C11 M10 -> M6-B rank-driven handoff passed");
            Console.WriteLine($"M10 formations={competition.Count} | M6-B budgets={result.M6BFormationBudgets.Count} | winner={result.M10.BestPlan.Formation}"); Console.WriteLine("PASS: C11 M10 -> M6-B rank-driven handoff"); return 0;
        } catch(Exception ex){MotorRunLogStore.Finish(runId,false,ex.Message);return Fail("C11 exception: "+ex.Message);}
    }
    private static int IndexOf<T>(IReadOnlyList<T> source,Func<T,bool> predicate){for(var i=0;i<source.Count;i++)if(predicate(source[i]))return i;return -1;}
    private static Player ReadPlayer(JsonElement e)=>new(e.GetProperty("id").GetInt32(),e.GetProperty("name").GetString()??"Player",e.GetProperty("keeper").GetInt32(),e.GetProperty("defending").GetInt32(),e.GetProperty("playmaking").GetInt32(),e.GetProperty("passing").GetInt32(),e.GetProperty("winger").GetInt32(),e.GetProperty("scoring").GetInt32(),e.GetProperty("stamina").GetInt32(),e.GetProperty("form").GetInt32(),e.GetProperty("experience").GetInt32(),GetInt(e,"loyalty",0),GetInt(e,"injuryLevel",-1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e){var ld=GetDouble(e,"leftDefence");var cd=GetDouble(e,"centralDefence");var rd=GetDouble(e,"rightDefence");var mid=GetDouble(e,"midfield");var la=GetDouble(e,"leftAttack");var ca=GetDouble(e,"centralAttack");var ra=GetDouble(e,"rightAttack");return new RegionalRatingSnapshot(ld,cd,rd,mid,la,ca,ra,ld,cd,rd,mid,la,ca,ra);}
    private static string GetString(JsonElement e,string n,string f)=>e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??f:f; private static int GetInt(JsonElement e,string n,int f)=>e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var x)?x:f; private static double GetDouble(JsonElement e,string n)=>e.GetProperty(n).GetDouble(); private static void Check(bool ok,string m){if(!ok)throw new InvalidOperationException(m);} private static int Fail(string m){Console.WriteLine("FAIL: "+m);return 1;}
}
