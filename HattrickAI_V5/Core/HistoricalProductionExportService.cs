using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

/// <summary>User-triggered collector for the user's own historical senior-team CHPP matches.</summary>
public sealed class HistoricalProductionExportService
{
    private const int DefaultTarget = 300;
    private const int ArchivePageSize = 50;
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromSeconds(1);
    private readonly ChppV5 _chpp;

    public HistoricalProductionExportService(ChppV5 chpp) => _chpp = chpp;

    public async Task<object> ExportAsync(string build, CancellationToken ct)
    {
        if (!_chpp.Connected) throw new UnauthorizedAccessException("CHPP bağlantısı yok.");
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"] = "3.0" }, ct);
        var team = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(team, "TeamID");
        var teamName = XmlV5.Text(team, "TeamName");
        if (teamId <= 0) throw new InvalidOperationException("CHPP takım ID'si alınamadı.");

        var target = DefaultTarget;
        var now = DateTimeOffset.UtcNow;
        var cursor = now.AddYears(-8);
        var archive = new Dictionary<int, ArchiveMatch>();
        var archiveRequests = 0;
        while (archive.Count < target && archiveRequests < 24 && cursor < now)
        {
            ct.ThrowIfCancellationRequested();
            var xml = await _chpp.GetXmlAsync("matchesArchive", new Dictionary<string,string?>
            {
                ["version"] = "1.0", ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture),
                ["FirstMatchDate"] = cursor.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ["LastMatchDate"] = now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            }, ct);
            archiveRequests++;
            var page = ReadArchive(xml);
            foreach (var m in page) archive[m.MatchId] = m;
            if (page.Count == 0) break;
            var newest = page.Max(x => x.Date);
            if (newest <= cursor) break;
            cursor = newest.AddSeconds(1);
            await Task.Delay(RequestSpacing, ct);
            if (page.Count < ArchivePageSize) break;
        }

        var selected = archive.Values.OrderByDescending(x => x.Date).Take(target).OrderBy(x => x.Date).ToArray();
        var rows = new List<object>(selected.Length);
        var detailSuccess = 0;
        var detailErrors = 0;
        foreach (var match in selected)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var xml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?>
                {
                    ["version"] = "1.4", ["matchID"] = match.MatchId.ToString(CultureInfo.InvariantCulture)
                }, ct);
                rows.Add(ParseDetail(xml, match, teamId));
                detailSuccess++;
            }
            catch (Exception ex)
            {
                rows.Add(new { matchId = match.MatchId, matchDate = match.Date, homeTeamId = match.HomeTeamId, awayTeamId = match.AwayTeamId, homeTeam = match.HomeTeamName, awayTeam = match.AwayTeamName, matchType = match.MatchType, homeGoals = match.HomeGoals, awayGoals = match.AwayGoals, error = ex.Message });
                detailErrors++;
            }
            await Task.Delay(RequestSpacing, ct);
        }

        return new
        {
            schema = "hattrickai-v5-historical-production-v1", exportedAt = DateTimeOffset.UtcNow, build, source = "CHPP",
            purpose = "V5 historical multi-match production acceptance corpus",
            collection = new { teamId, teamName, requestedMatches = target, archiveUniqueMatchCount = archive.Count, selectedMatches = selected.Length, detailSuccess, detailErrors, archiveRequests, chppRequestsAreSequential = true, requestSpacingSeconds = RequestSpacing.TotalSeconds },
            rows
        };
    }

    private static List<ArchiveMatch> ReadArchive(string xml)
    {
        var result = new List<ArchiveMatch>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m, "MatchID"); var date = XmlV5.Date(m, "MatchDate");
            var home = m.Element("HomeTeam"); var away = m.Element("AwayTeam");
            if (id <= 0 || date == default || home is null || away is null) continue;
            var status = XmlV5.Text(m, "Status");
            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("FINISHED", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new ArchiveMatch(id, date, XmlV5.Int(home, "HomeTeamID"), XmlV5.Int(away, "AwayTeamID"), XmlV5.Text(home, "HomeTeamName"), XmlV5.Text(away, "AwayTeamName"), XmlV5.Int(m, "MatchType"), XmlV5.Int(home, "HomeGoals"), XmlV5.Int(away, "AwayGoals")));
        }
        return result;
    }

    private static object ParseDetail(string xml, ArchiveMatch archive, int ownTeamId)
    {
        var root = XmlV5.Root(xml); var match = root?.Descendants("Match").FirstOrDefault();
        var home = match?.Element("HomeTeam"); var away = match?.Element("AwayTeam");
        if (match is null || home is null || away is null) throw new InvalidOperationException("matchdetails XML eksik.");
        var homeId = XmlV5.Int(home, "HomeTeamID"); var awayId = XmlV5.Int(away, "AwayTeamID");
        if (homeId != archive.HomeTeamId || awayId != archive.AwayTeamId) throw new InvalidOperationException("Archive/matchdetails takım kimliği uyuşmuyor.");
        var ownHome = ownTeamId == homeId; var own = ownHome ? home : away; var opp = ownHome ? away : home;
        var ownPoss = ownHome ? Average(XmlV5.Int(match,"PossessionFirstHalfHome"), XmlV5.Int(match,"PossessionSecondHalfHome")) : Average(XmlV5.Int(match,"PossessionFirstHalfAway"), XmlV5.Int(match,"PossessionSecondHalfAway"));
        var oppPoss = ownHome ? Average(XmlV5.Int(match,"PossessionFirstHalfAway"), XmlV5.Int(match,"PossessionSecondHalfAway")) : Average(XmlV5.Int(match,"PossessionFirstHalfHome"), XmlV5.Int(match,"PossessionSecondHalfHome"));
        return new
        {
            matchId = archive.MatchId, matchDate = archive.Date, matchType = archive.MatchType,
            homeTeamId = homeId, awayTeamId = awayId, homeTeam = XmlV5.Text(home,"HomeTeamName"), awayTeam = XmlV5.Text(away,"AwayTeamName"),
            homeGoals = XmlV5.Int(home,"HomeGoals"), awayGoals = XmlV5.Int(away,"AwayGoals"), ownIsHome = ownHome, ownTeamId, opponentTeamId = ownHome ? awayId : homeId,
            ownTactic = XmlV5.Int(own,"TacticType"), ownTacticSkill = XmlV5.Int(own,"TacticSkill"), opponentTactic = XmlV5.Int(opp,"TacticType"), opponentTacticSkill = XmlV5.Int(opp,"TacticSkill"),
            ownRatingMidfield = XmlV5.Int(own,"RatingMidfield"), opponentRatingMidfield = XmlV5.Int(opp,"RatingMidfield"),
            ownRatingLeftDef = XmlV5.Int(own,"RatingLeftDef"), opponentRatingLeftDef = XmlV5.Int(opp,"RatingLeftDef"), ownRatingMidDef = XmlV5.Int(own,"RatingMidDef"), opponentRatingMidDef = XmlV5.Int(opp,"RatingMidDef"), ownRatingRightDef = XmlV5.Int(own,"RatingRightDef"), opponentRatingRightDef = XmlV5.Int(opp,"RatingRightDef"),
            ownRatingLeftAtt = XmlV5.Int(own,"RatingLeftAtt"), opponentRatingLeftAtt = XmlV5.Int(opp,"RatingLeftAtt"), ownRatingMidAtt = XmlV5.Int(own,"RatingMidAtt"), opponentRatingMidAtt = XmlV5.Int(opp,"RatingMidAtt"), ownRatingRightAtt = XmlV5.Int(own,"RatingRightAtt"), opponentRatingRightAtt = XmlV5.Int(opp,"RatingRightAtt"),
            ownRatingIndirectSetPiecesDef = XmlV5.Int(own,"RatingIndirectSetPiecesDef"), ownRatingIndirectSetPiecesAtt = XmlV5.Int(own,"RatingIndirectSetPiecesAtt"), opponentRatingIndirectSetPiecesDef = XmlV5.Int(opp,"RatingIndirectSetPiecesDef"), opponentRatingIndirectSetPiecesAtt = XmlV5.Int(opp,"RatingIndirectSetPiecesAtt"),
            ownTeamAttitude = XmlV5.Int(own,"TeamAttitude"), ownPossessionPercent = ownPoss, opponentPossessionPercent = oppPoss,
            ownSectorChances = Chances(own), opponentSectorChances = Chances(opp), weatherId = XmlV5.Int(root?.Descendants("Arena").FirstOrDefault(),"WeatherID"), rawMatchDetailsXml = xml
        };
    }

    private static object Chances(XElement team) => new { left = XmlV5.Int(team,"NrOfChancesLeft"), center = XmlV5.Int(team,"NrOfChancesCenter"), right = XmlV5.Int(team,"NrOfChancesRight"), specialEvents = XmlV5.Int(team,"NrOfChancesSpecialEvents"), other = XmlV5.Int(team,"NrOfChancesOther") };
    private static double Average(int a, int b) => (a + b) / 2.0;
    private sealed record ArchiveMatch(int MatchId, DateTimeOffset Date, int HomeTeamId, int AwayTeamId, string HomeTeamName, string AwayTeamName, int MatchType, int HomeGoals, int AwayGoals);
}
