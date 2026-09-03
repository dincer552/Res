using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace HattrickAI.V5.Core;

public sealed class ReferenceMatchService
{
    private readonly ChppV5 _chpp;
    private readonly IHttpContextAccessor _http;
    public ReferenceMatchService(ChppV5 chpp, IHttpContextAccessor http) { _chpp = chpp; _http = http; }

    public async Task<object> GetAsync(CancellationToken ct)
    {
        if (string.Equals(_http.HttpContext?.Request.Query["calibration"], "1", StringComparison.Ordinal))
            return await CollectCalibration(ct);

        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");
        var ownLogoUrl = teamNode?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        var ownMatches = await ReadMatches(teamId, ct);
        var now = DateTimeOffset.UtcNow;
        var upcomingLeague = ownMatches.Where(m=>m.Date>now&&m.MatchType==1).OrderBy(m=>m.Date).Select(m=>new {matchId=m.MatchId,date=m.Date,homeTeam=m.HomeTeam,homeTeamId=m.HomeTeamId,awayTeam=m.AwayTeam,awayTeamId=m.AwayTeamId,matchType=m.MatchType,matchTypeName=m.MatchTypeName,isHome=m.HomeTeamId==teamId,opponentTeam=m.HomeTeamId==teamId?m.AwayTeam:m.HomeTeam,opponentTeamId=m.HomeTeamId==teamId?m.AwayTeamId:m.HomeTeamId,opponentLogoUrl=string.Empty}).ToList();
        var logoCache=new Dictionary<int,string>(); foreach(var match in upcomingLeague) if(!logoCache.ContainsKey(match.opponentTeamId)) logoCache[match.opponentTeamId]=await ReadLogoUrl(match.opponentTeamId,ct);
        var upcomingWithLogos=upcomingLeague.Select(m=>new {m.matchId,m.date,m.homeTeam,m.homeTeamId,m.awayTeam,m.awayTeamId,m.matchType,m.matchTypeName,m.isHome,m.opponentTeam,m.opponentTeamId,opponentLogoUrl=logoCache.GetValueOrDefault(m.opponentTeamId,string.Empty)}).ToList();
        var next=ownMatches.Where(m=>m.Date>now&&IsCompetitiveMatchType(m.MatchType)).OrderBy(m=>m.Date).FirstOrDefault();
        if(next is null) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra yaklaşan resmi maç bulunamadı.");
        var opponentId=next.HomeTeamId==teamId?next.AwayTeamId:next.HomeTeamId; var opponentMatches=await ReadMatches(opponentId,ct);
        var last=opponentMatches.Where(m=>m.Date<now&&m.HomeGoals.HasValue&&m.AwayGoals.HasValue&&IsCompetitiveMatchType(m.MatchType)).OrderByDescending(m=>m.Date).FirstOrDefault();
        if(last is null) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra rakibin baz alınan resmi maçı bulunamadı.");
        return new {matchId=last.MatchId,date=last.Date,homeTeam=last.HomeTeam,awayTeam=last.AwayTeam,homeGoals=last.HomeGoals,awayGoals=last.AwayGoals,matchType=last.MatchType,matchTypeName=last.MatchTypeName,opponentTeam=opponentId==last.HomeTeamId?last.HomeTeam:last.AwayTeam,opponentWasHome=opponentId==last.HomeTeamId,finished=last.HomeGoals.HasValue&&last.AwayGoals.HasValue,ownLogoUrl,upcomingMatches=upcomingWithLogos,opponentLogoUrl=logoCache.GetValueOrDefault(opponentId,string.Empty)};
    }

    private async Task<object> CollectCalibration(CancellationToken ct)
    {
        var teamXml=await _chpp.GetXmlAsync("teamdetails",new Dictionary<string,string?>{{"version","3.0"}},ct);
        var team=XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault(); var teamId=XmlV5.Int(team,"TeamID");
        if(teamId<=0) throw new InvalidOperationException("Kullanıcı takım ID'si alınamadı.");
        var limit=Math.Clamp(int.TryParse(_http.HttpContext?.Request.Query["limit"],out var n)?n:40,5,50);
        var matches=await ReadArchive(teamId,DateTimeOffset.UtcNow.AddMonths(-12),DateTimeOffset.UtcNow,ct);
        var eligible=matches.Where(m=>m.Date<DateTimeOffset.UtcNow&&m.HomeGoals.HasValue&&m.AwayGoals.HasValue&&IsCompetitiveMatchType(m.MatchType)).OrderByDescending(m=>m.Date).Take(limit).ToList();
        var rows=new List<object>(); var detailsFetched=0; var chanceSamples=0; var totalSector=0; double possessionSum=0;
        foreach(var m in eligible){
            try{
                var xml=await _chpp.GetXmlAsync("matchdetails",new Dictionary<string,string?>{{"version","3.1"},{"matchID",m.MatchId.ToString(CultureInfo.InvariantCulture)},{"matchEvents","true"}},ct);
                var root=XmlV5.Root(xml); var node=root?.Descendants("Match").FirstOrDefault(); if(node is null) continue; detailsFetched++;
                var home=node.Descendants("HomeTeam").FirstOrDefault(); var away=node.Descendants("AwayTeam").FirstOrDefault();
                var hp=XmlV5.Int(node,"PossessionFirstHalfHome"); var ap=XmlV5.Int(node,"PossessionFirstHalfAway"); var hp2=XmlV5.Int(node,"PossessionSecondHalfHome"); var ap2=XmlV5.Int(node,"PossessionSecondHalfAway");
                var ownHome=m.HomeTeamId==teamId; var ownPoss=ownHome?(hp+hp2)/2.0:(ap+ap2)/2.0; var hs=XmlV5.Int(home,"NrOfChancesLeft")+XmlV5.Int(home,"NrOfChancesCenter")+XmlV5.Int(home,"NrOfChancesRight"); var @as=XmlV5.Int(away,"NrOfChancesLeft")+XmlV5.Int(away,"NrOfChancesCenter")+XmlV5.Int(away,"NrOfChancesRight"); var ownSector=ownHome?hs:@as; var oppSector=ownHome?@as:hs; totalSector+=ownSector+oppSector; possessionSum+=ownPoss; chanceSamples++;
                rows.Add(new {matchId=m.MatchId,date=m.Date,isHome=ownHome,ownPossessionPercent=Math.Round(ownPoss,2),homeSectorChances=hs,awaySectorChances=@as,ownSectorChances=ownSector,opponentSectorChances=oppSector,homeOtherChances=XmlV5.Int(home,"NrOfChancesOther"),awayOtherChances=XmlV5.Int(away,"NrOfChancesOther"),homeSpecialEventChances=XmlV5.Int(home,"NrOfChancesSpecialEvents"),awaySpecialEventChances=XmlV5.Int(away,"NrOfChancesSpecialEvents"),homeGoals=m.HomeGoals,awayGoals=m.AwayGoals,homeTactic=XmlV5.Int(home,"TacticType"),awayTactic=XmlV5.Int(away,"TacticType")});
            }catch(Exception ex){ rows.Add(new {matchId=m.MatchId,date=m.Date,error=ex.Message}); }
        }
        return new {ok=true,phase="D",sampleCount=eligible.Count,detailsFetched,chanceSamples,meanOwnPossessionPercent=chanceSamples==0?0:Math.Round(possessionSum/chanceSamples,2),totalObservedSectorChances=totalSector,notes="Observation-only. Sector chances are collected separately from Other/Special Event counts; production M8 coefficients are unchanged.",rows};
    }

    private async Task<List<ReferenceMatch>> ReadArchive(int teamId,DateTimeOffset first,DateTimeOffset last,CancellationToken ct){
        var xml=await _chpp.GetXmlAsync("matchesarchive",new Dictionary<string,string?>{{"version","1.5"},{"teamID",teamId.ToString(CultureInfo.InvariantCulture)},{"FirstMatchDate",first.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)},{"LastMatchDate",last.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)}},ct);
        var result=new List<ReferenceMatch>(); foreach(var m in XmlV5.Root(xml)?.Descendants("Match")??Enumerable.Empty<XElement>()){var id=XmlV5.Int(m,"MatchID");var date=XmlV5.Date(m,"MatchDate");var hi=XmlV5.Int(m,"HomeTeamID");var ai=XmlV5.Int(m,"AwayTeamID");if(id<=0||date==default)continue;var type=XmlV5.Int(m,"MatchType");result.Add(new ReferenceMatch(id,date,XmlV5.Text(m,"HomeTeamName"),hi,XmlV5.Text(m,"AwayTeamName"),ai,NullableInt(m,"HomeGoals"),NullableInt(m,"AwayGoals"),type,MatchTypeName(type)));} return result;
    }
    private async Task<string> ReadLogoUrl(int teamId,CancellationToken ct){if(teamId<=0)return string.Empty;try{var xml=await _chpp.GetXmlAsync("teamdetails",new Dictionary<string,string?>{{"version","3.0"},{"teamID",teamId.ToString(CultureInfo.InvariantCulture)}},ct);return XmlV5.Root(xml)?.Descendants("Team").FirstOrDefault()?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim()??string.Empty;}catch{return string.Empty;}}
    private static bool IsCompetitiveMatchType(int type)=>type is 1 or 2 or 7 or 10 or 11;
    private async Task<List<ReferenceMatch>> ReadMatches(int teamId,CancellationToken ct){var xml=await _chpp.GetXmlAsync("matches",new Dictionary<string,string?>{{"version","1.3"},{"teamId",teamId.ToString(CultureInfo.InvariantCulture)}},ct);var result=new List<ReferenceMatch>();foreach(var m in XmlV5.Root(xml)?.Descendants("Match")??Enumerable.Empty<XElement>()){var id=XmlV5.Int(m,"MatchID");var date=XmlV5.Date(m,"MatchDate");var hi=XmlV5.Int(m,"HomeTeamID");var ai=XmlV5.Int(m,"AwayTeamID");if(id<=0||date==default||(hi!=teamId&&ai!=teamId))continue;var type=XmlV5.Int(m,"MatchType");result.Add(new ReferenceMatch(id,date,XmlV5.Text(m,"HomeTeamName"),hi,XmlV5.Text(m,"AwayTeamName"),ai,NullableInt(m,"HomeGoals"),NullableInt(m,"AwayGoals"),type,MatchTypeName(type)));}return result;}
    private static int? NullableInt(XElement? e,string name){var text=XmlV5.Text(e,name);return int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)?value:null;}
    private static string MatchTypeName(int type)=>type switch{1=>"Lig",2=>"Play-off / Qualification",3=>"Kupa",4=>"Hazırlık",5=>"Hazırlık (kupa kuralları)",7=>"Hattrick Masters",8=>"Uluslararası hazırlık",9=>"Uluslararası hazırlık (kupa kuralları)",10=>"Milli takım resmi",11=>"Milli takım resmi (kupa kuralları)",12=>"Milli takım hazırlık",_=>$"Maç türü {type}"};
    private sealed record ReferenceMatch(int MatchId,DateTimeOffset Date,string HomeTeam,int HomeTeamId,string AwayTeam,int AwayTeamId,int? HomeGoals,int? AwayGoals,int MatchType,string MatchTypeName);
}