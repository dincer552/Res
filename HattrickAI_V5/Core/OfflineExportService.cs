using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Offline test exportu için CHPP'den gerekli ham ve normalize edilmiş veriyi toplar.
/// OAuth bilgileri/tokenları hiçbir zaman export edilmez.
/// </summary>
public sealed class OfflineExportService
{
    private readonly ChppV5 _chpp;
    private readonly AnalysisService _analysis;
    private readonly PlayerAnalysisEngine _playerAnalysis = new();

    public OfflineExportService(ChppV5 chpp, AnalysisService analysis)
    {
        _chpp = chpp;
        _analysis = analysis;
    }

    public async Task<object> ExportAsync(string build, MatchQuestionnaire questionnaire, CancellationToken ct)
    {
        if (!_chpp.Connected) throw new UnauthorizedAccessException("CHPP bağlantısı yok.");
        if (_chpp.HistoricalProductionExportRequested)
            return await new HistoricalProductionExportService(_chpp).ExportAsync(build, ct);

        var analysis = await _analysis.RunAsync(build, questionnaire, ct);
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"] = "3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var ownTeamId = XmlV5.Int(teamNode, "TeamID");
        if (ownTeamId <= 0) throw new InvalidOperationException("CHPP takım ID'si alınamadı.");
        var trainingXml = await _chpp.GetXmlAsync("training", new Dictionary<string,string?> { ["version"] = "1.1" }, ct);
        var ownPlayersXml = await _chpp.GetXmlAsync("players", new Dictionary<string,string?> { ["version"] = "1.3", ["teamId"] = ownTeamId.ToString(CultureInfo.InvariantCulture) }, ct);
        var ownPlayers = ReadPlayers(ownPlayersXml);
        var ownProfiles = ownPlayers.Select(_playerAnalysis.AnalyzePlayer).ToList();
        var matchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"] = "2.2", ["teamID"] = ownTeamId.ToString(CultureInfo.InvariantCulture) }, ct);
        var matches = ReadMatches(matchesXml, ownTeamId);
        var now = DateTimeOffset.UtcNow;
        var next = matches.Where(x => x.Date > now && IsCompetitiveMatchType(x.MatchType)).OrderBy(x => x.Date).FirstOrDefault();
        if (next.MatchId <= 0) throw new InvalidOperationException("Yaklaşan resmi maç bulunamadı.");
        var opponentId = next.HomeId == ownTeamId ? next.AwayId : next.HomeId;
        var opponentMatchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"] = "2.2", ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture) }, ct);
        var opponentMatches = ReadMatches(opponentMatchesXml, opponentId);
        var last = opponentMatches.Where(x => x.Date != default && x.Date <= now && IsCompetitiveMatchType(x.MatchType)).OrderByDescending(x => x.Date).FirstOrDefault();
        if (last.MatchId <= 0) throw new InvalidOperationException("Rakibin son resmi maçı bulunamadı.");
        var lineupXml = await _chpp.GetXmlAsync("matchlineup", new Dictionary<string,string?> { ["version"] = "1.1", ["actionType"] = "view", ["matchID"] = last.MatchId.ToString(CultureInfo.InvariantCulture), ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture) }, ct);
        var matchDetailsXml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?> { ["version"] = "1.4", ["matchID"] = last.MatchId.ToString(CultureInfo.InvariantCulture) }, ct);
        string? opponentPlayersXml = null; string? opponentPlayersError = null;
        try { opponentPlayersXml = await _chpp.GetXmlAsync("players", new Dictionary<string,string?> { ["version"] = "1.3", ["teamId"] = opponentId.ToString(CultureInfo.InvariantCulture) }, ct); }
        catch (Exception ex) { opponentPlayersError = ex.Message; }
        return new
        {
            schema = "hattrickai-v5-offline-test-v2", exportedAt = DateTimeOffset.UtcNow, build, source = "CHPP",
            security = new { credentialsIncluded = false, oauthTokensIncluded = false, sessionCookiesIncluded = false },
            purpose = "V5 motorlarını offline/regression test etmek",
            match = new { ownTeamId, ownTeam = analysis.TeamName, opponentTeamId = opponentId, opponentTeam = analysis.OpponentName,
                nextMatch = new { next.MatchId, next.Date, next.HomeId, next.AwayId, next.HomeName, next.AwayName, next.MatchType },
                opponentReferenceMatch = new { last.MatchId, last.Date, last.HomeId, last.AwayId, last.HomeName, last.AwayName, last.MatchType } },
            questionnaire,
            rawChpp = new { teamDetails = teamXml, training = trainingXml, ownPlayers = ownPlayersXml, ownMatches = matchesXml, opponentMatches = opponentMatchesXml, opponentLastLineup = lineupXml, opponentLastMatchDetails = matchDetailsXml, opponentPlayers = opponentPlayersXml, opponentPlayersError },
            normalized = new { ownPlayers, ownPlayerAnalysis = ownProfiles, opponentLastLineup = ParseLineup(lineupXml), opponentLastMatchRatings = ParseMatchRatings(matchDetailsXml, opponentId) },
            v5Analysis = analysis
        };
    }

    private static bool IsCompetitiveMatchType(int type) => type is 1 or 2 or 7 or 10 or 11;

    private static List<Player> ReadPlayers(string xml)
        => XmlV5.Root(xml)?.Descendants("Player").Select(p => new Player(
            XmlV5.Int(p,"PlayerID"), XmlV5.Text(p,"PlayerName"), XmlV5.Int(p,"KeeperSkill"), XmlV5.Int(p,"DefenderSkill"), XmlV5.Int(p,"PlaymakerSkill"),
            XmlV5.Int(p,"PassingSkill"), XmlV5.Int(p,"WingerSkill"), XmlV5.Int(p,"ScorerSkill"), XmlV5.Int(p,"StaminaSkill"), XmlV5.Int(p,"PlayerForm"),
            XmlV5.Int(p,"Experience"), XmlV5.Int(p,"Loyalty"), XmlV5.Int(p,"InjuryLevel"))).Where(p => p.Id > 0).ToList() ?? new();

    private static List<(int MatchId,DateTimeOffset Date,int HomeId,int AwayId,string HomeName,string AwayName,int MatchType)> ReadMatches(string xml, int teamId)
    {
        var result = new List<(int,DateTimeOffset,int,int,string,string,int)>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m,"MatchID"); var date = XmlV5.Date(m,"MatchDate"); var home = XmlV5.Int(m,"HomeTeamID"); var away = XmlV5.Int(m,"AwayTeamID"); var type = XmlV5.Int(m,"MatchType");
            if (id > 0 && date != default && (home == teamId || away == teamId)) result.Add((id,date,home,away,XmlV5.Text(m,"HomeTeamName"),XmlV5.Text(m,"AwayTeamName"),type));
        }
        return result;
    }

    private static object ParseLineup(string xml)
    {
        var root = XmlV5.Root(xml); var team = root?.Descendants("Team").FirstOrDefault();
        var players = team?.Element("Lineup")?.Elements("Player").Select(p => new { playerId = XmlV5.Int(p,"PlayerID"), playerName = XmlV5.Text(p,"PlayerName"), positionCode = XmlV5.Int(p,"PositionCode"), roleId = XmlV5.Int(p,"RoleID"), behaviour = XmlV5.Int(p,"Behaviour"), ratingStars = XmlV5.Text(p,"RatingStars") }).Where(x => x.playerId > 0).ToList() ?? new();
        return new { matchId = XmlV5.Int(root,"MatchID"), teamId = TeamNodeId(team), players };
    }

    private static object ParseMatchRatings(string xml, int opponentId)
    {
        var match = XmlV5.Root(xml)?.Descendants("Match").FirstOrDefault(); var team = match?.Element("HomeTeam");
        if (team is null || TeamNodeId(team) != opponentId) team = match?.Element("AwayTeam");
        if (team is null || TeamNodeId(team) != opponentId) return new { available = false };
        return new { available = true, leftDefence = XmlV5.Int(team,"RatingLeftDef") / 4.0, centralDefence = XmlV5.Int(team,"RatingMidDef") / 4.0, rightDefence = XmlV5.Int(team,"RatingRightDef") / 4.0, midfield = XmlV5.Int(team,"RatingMidfield") / 4.0, leftAttack = XmlV5.Int(team,"RatingLeftAtt") / 4.0, centralAttack = XmlV5.Int(team,"RatingMidAtt") / 4.0, rightAttack = XmlV5.Int(team,"RatingRightAtt") / 4.0 };
    }

    private static int TeamNodeId(XElement? node) => node?.Name.LocalName switch { "HomeTeam" => XmlV5.Int(node,"HomeTeamID"), "AwayTeam" => XmlV5.Int(node,"AwayTeamID"), "Team" => XmlV5.Int(node,"TeamID"), _ => 0 };
}
