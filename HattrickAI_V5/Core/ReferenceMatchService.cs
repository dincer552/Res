using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed class ReferenceMatchService
{
    private readonly ChppV5 _chpp;
    public ReferenceMatchService(ChppV5 chpp) => _chpp = chpp;

    public async Task<object> GetAsync(CancellationToken ct)
    {
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");
        var ownLogoUrl = XmlV5.Text(teamNode?.Descendants("LogoURL").FirstOrDefault(), "LogoURL");
        if (string.IsNullOrWhiteSpace(ownLogoUrl)) ownLogoUrl = teamNode?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim() ?? string.Empty;

        var ownMatches = await ReadMatches(teamId, ct);
        var now = DateTimeOffset.UtcNow;
        var upcomingLeague = ownMatches
            .Where(m => m.Date > now && m.MatchType == 1)
            .OrderBy(m => m.Date)
            .Select(m => new
            {
                matchId = m.MatchId,
                date = m.Date,
                homeTeam = m.HomeTeam,
                homeTeamId = m.HomeTeamId,
                awayTeam = m.AwayTeam,
                awayTeamId = m.AwayTeamId,
                matchType = m.MatchType,
                matchTypeName = m.MatchTypeName,
                isHome = m.HomeTeamId == teamId,
                opponentTeam = m.HomeTeamId == teamId ? m.AwayTeam : m.HomeTeam,
                opponentTeamId = m.HomeTeamId == teamId ? m.AwayTeamId : m.HomeTeamId,
                opponentLogoUrl = string.Empty
            })
            .ToList();

        var logoCache = new Dictionary<int,string>();
        foreach (var match in upcomingLeague)
        {
            if (!logoCache.ContainsKey(match.opponentTeamId))
                logoCache[match.opponentTeamId] = await ReadLogoUrl(match.opponentTeamId, ct);
        }
        var upcomingWithLogos = upcomingLeague.Select(m => new
        {
            m.matchId, m.date, m.homeTeam, m.homeTeamId, m.awayTeam, m.awayTeamId, m.matchType, m.matchTypeName,
            m.isHome, m.opponentTeam, m.opponentTeamId,
            opponentLogoUrl = logoCache.GetValueOrDefault(m.opponentTeamId, string.Empty)
        }).ToList();

        var next = ownMatches
            .Where(m => m.Date > now && IsCompetitiveMatchType(m.MatchType))
            .OrderBy(m => m.Date)
            .FirstOrDefault();
        if (next is null)
            throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra yaklaşan resmi maç bulunamadı.");

        var opponentId = next.HomeTeamId == teamId ? next.AwayTeamId : next.HomeTeamId;
        if (opponentId <= 0) throw new InvalidOperationException("Rakip takım ID'si bulunamadı.");

        var opponentMatches = await ReadMatches(opponentId, ct);
        var last = opponentMatches
            .Where(m => m.Date < now && m.HomeGoals.HasValue && m.AwayGoals.HasValue && IsCompetitiveMatchType(m.MatchType))
            .OrderByDescending(m => m.Date)
            .FirstOrDefault();
        if (last is null)
            throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra rakibin baz alınan resmi maçı bulunamadı.");

        return new
        {
            matchId = last.MatchId,
            date = last.Date,
            homeTeam = last.HomeTeam,
            awayTeam = last.AwayTeam,
            homeGoals = last.HomeGoals,
            awayGoals = last.AwayGoals,
            matchType = last.MatchType,
            matchTypeName = last.MatchTypeName,
            opponentTeam = opponentId == last.HomeTeamId ? last.HomeTeam : last.AwayTeam,
            opponentWasHome = opponentId == last.HomeTeamId,
            finished = last.HomeGoals.HasValue && last.AwayGoals.HasValue,
            ownLogoUrl,
            upcomingMatches = upcomingWithLogos,
            opponentLogoUrl = logoCache.GetValueOrDefault(opponentId, string.Empty)
        };
    }

    private async Task<string> ReadLogoUrl(int teamId, CancellationToken ct)
    {
        if (teamId <= 0) return string.Empty;
        try
        {
            var xml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?>
            {
                ["version"]="3.0",
                ["teamID"]=teamId.ToString(CultureInfo.InvariantCulture)
            }, ct);
            var team = XmlV5.Root(xml)?.Descendants("Team").FirstOrDefault();
            return team?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static bool IsCompetitiveMatchType(int type) => type is 1 or 2 or 7 or 10 or 11;

    private async Task<List<ReferenceMatch>> ReadMatches(int teamId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?>
        {
            ["version"]="1.3",
            ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var result = new List<ReferenceMatch>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m,"MatchID");
            var date = XmlV5.Date(m,"MatchDate");
            var homeId = XmlV5.Int(m,"HomeTeamID");
            var awayId = XmlV5.Int(m,"AwayTeamID");
            if (id <= 0 || date == default || (homeId != teamId && awayId != teamId)) continue;
            var type = XmlV5.Int(m,"MatchType");
            result.Add(new ReferenceMatch(id,date,XmlV5.Text(m,"HomeTeamName"),homeId,XmlV5.Text(m,"AwayTeamName"),awayId,NullableInt(m,"HomeGoals"),NullableInt(m,"AwayGoals"),type,MatchTypeName(type)));
        }
        return result;
    }

    private static int? NullableInt(XElement? e, string name)
    {
        var text = XmlV5.Text(e, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string MatchTypeName(int type) => type switch
    {
        1 => "Lig", 2 => "Play-off / Qualification", 3 => "Kupa", 4 => "Hazırlık", 5 => "Hazırlık (kupa kuralları)",
        7 => "Hattrick Masters", 8 => "Uluslararası hazırlık", 9 => "Uluslararası hazırlık (kupa kuralları)",
        10 => "Milli takım resmi", 11 => "Milli takım resmi (kupa kuralları)", 12 => "Milli takım hazırlık", _ => $"Maç türü {type}"
    };

    private sealed record ReferenceMatch(int MatchId,DateTimeOffset Date,string HomeTeam,int HomeTeamId,string AwayTeam,int AwayTeamId,int? HomeGoals,int? AwayGoals,int MatchType,string MatchTypeName);
}
