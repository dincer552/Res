using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed class Phase1ChppValidationService
{
    private readonly ChppV5 _chpp;
    public Phase1ChppValidationService(ChppV5 chpp) => _chpp = chpp;

    public async Task<object> GetAsync(CancellationToken ct)
    {
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var team = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(team, "TeamID");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");

        var matchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"]="1.3", ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture) }, ct);
        var matches = ReadMatches(matchesXml, teamId);
        var next = matches.Where(x => x.Date > DateTimeOffset.UtcNow).OrderBy(x => x.Date).FirstOrDefault();
        if (next.MatchId <= 0) throw new InvalidOperationException("Yaklaşan maç bulunamadı.");

        var opponentId = next.HomeId == teamId ? next.AwayId : next.HomeId;
        var opponentMatchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"]="1.3", ["teamId"]=opponentId.ToString(CultureInfo.InvariantCulture) }, ct);
        var opponentMatches = ReadMatches(opponentMatchesXml, opponentId);
        var last = opponentMatches.Where(x => x.Date < DateTimeOffset.UtcNow).OrderByDescending(x => x.Date).FirstOrDefault();
        if (last.MatchId <= 0) throw new InvalidOperationException("Rakibin son maçı bulunamadı.");

        var detailsXml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?> { ["version"]="1.4", ["matchID"]=last.MatchId.ToString(CultureInfo.InvariantCulture) }, ct);
        var match = XmlV5.Root(detailsXml)?.Descendants("Match").FirstOrDefault();
        var teamNode = match?.Elements().FirstOrDefault(x => XmlV5.Int(x,"HomeTeamID") == opponentId || XmlV5.Int(x,"AwayTeamID") == opponentId);
        if (teamNode is null)
            teamNode = match?.Descendants().FirstOrDefault(x => XmlV5.Int(x,"HomeTeamID") == opponentId || XmlV5.Int(x,"AwayTeamID") == opponentId);
        if (teamNode is null) throw new InvalidOperationException("Baz maçın CHPP rating bölümü bulunamadı.");

        var lineupXml = await _chpp.GetXmlAsync("matchlineup", new Dictionary<string,string?>
        {
            ["version"]="1.1",
            ["matchID"]=last.MatchId.ToString(CultureInfo.InvariantCulture),
            ["teamID"]=opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var players = XmlV5.Root(lineupXml)?.Descendants("Player").Where(p => XmlV5.Int(p,"PositionCode") > 0).Take(11)
            .Select(p => new
            {
                id = XmlV5.Int(p,"PlayerID"),
                name = XmlV5.Text(p,"PlayerName"),
                positionCode = XmlV5.Int(p,"PositionCode"),
                behaviour = XmlV5.Int(p,"Behaviour")
            }).ToArray() ?? Array.Empty<object>();

        return new
        {
            teamName = XmlV5.Text(teamNode,"HomeTeamID") == opponentId.ToString(CultureInfo.InvariantCulture) ? XmlV5.Text(teamNode,"HomeTeamName") : XmlV5.Text(teamNode,"AwayTeamName"),
            opponentId,
            referenceMatch = new
            {
                matchId = last.MatchId,
                date = last.Date,
                homeTeam = last.HomeName,
                awayTeam = last.AwayName,
                homeGoals = last.HomeGoals,
                awayGoals = last.AwayGoals,
                matchType = last.MatchTypeName
            },
            chpp = new
            {
                leftDefence = ToDisplay(XmlV5.Int(teamNode,"RatingLeftDef")),
                centralDefence = ToDisplay(XmlV5.Int(teamNode,"RatingMidDef")),
                rightDefence = ToDisplay(XmlV5.Int(teamNode,"RatingRightDef")),
                midfield = ToDisplay(XmlV5.Int(teamNode,"RatingMidfield")),
                leftAttack = ToDisplay(XmlV5.Int(teamNode,"RatingLeftAtt")),
                centralAttack = ToDisplay(XmlV5.Int(teamNode,"RatingMidAtt")),
                rightAttack = ToDisplay(XmlV5.Int(teamNode,"RatingRightAtt"))
            },
            rawChpp = new
            {
                leftDefence = XmlV5.Int(teamNode,"RatingLeftDef"),
                centralDefence = XmlV5.Int(teamNode,"RatingMidDef"),
                rightDefence = XmlV5.Int(teamNode,"RatingRightDef"),
                midfield = XmlV5.Int(teamNode,"RatingMidfield"),
                leftAttack = XmlV5.Int(teamNode,"RatingLeftAtt"),
                centralAttack = XmlV5.Int(teamNode,"RatingMidAtt"),
                rightAttack = XmlV5.Int(teamNode,"RatingRightAtt")
            },
            lineup = players
        };
    }

    private static double ToDisplay(int rating) => rating <= 0 ? 0 : (rating - 1) / 4.0 + 0.25;

    private static List<MatchRow> ReadMatches(string xml, int teamId)
    {
        var result = new List<MatchRow>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m,"MatchID");
            var date = XmlV5.Date(m,"MatchDate");
            var home = XmlV5.Int(m,"HomeTeamID");
            var away = XmlV5.Int(m,"AwayTeamID");
            if (id <= 0 || date == default || (home != teamId && away != teamId)) continue;
            var type = XmlV5.Int(m,"MatchType");
            result.Add(new MatchRow(id,date,home,away,XmlV5.Text(m,"HomeTeamName"),XmlV5.Text(m,"AwayTeamName"),NullableInt(m,"HomeGoals"),NullableInt(m,"AwayGoals"),MatchTypeName(type)));
        }
        return result;
    }

    private static int? NullableInt(XElement? e,string name)
        => int.TryParse(XmlV5.Text(e,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out var v) ? v : null;

    private static string MatchTypeName(int type) => type switch
    {
        1 => "Lig", 2 => "Play-off / Qualification", 3 => "Kupa", 4 => "Hazırlık", 5 => "Hazırlık (kupa kuralları)",
        7 => "Hattrick Masters", 8 => "Uluslararası hazırlık", 9 => "Uluslararası hazırlık (kupa kuralları)",
        10 => "Milli takım resmi", 11 => "Milli takım resmi (kupa kuralları)", 12 => "Milli takım hazırlık", _ => $"Maç türü {type}"
    };

    private sealed record MatchRow(int MatchId,DateTimeOffset Date,int HomeId,int AwayId,string HomeName,string AwayName,int? HomeGoals,int? AwayGoals,string MatchTypeName);
}
