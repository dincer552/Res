using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed class AnalysisService
{
    private readonly ChppV5 _chpp;
    private readonly RegionalRatingEngineFixed _ratingEngine = new();
    private readonly XIOptimizationService _xiOptimization = new();
    private readonly OpponentThreatEngine _threatEngine = new();
    public AnalysisService(ChppV5 chpp) => _chpp = chpp;

    public async Task<Analysis> RunAsync(string build, MatchQuestionnaire? questionnaire, CancellationToken ct)
    {
        questionnaire ??= MatchQuestionnaire.Default;

        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        var teamName = XmlV5.Text(teamNode, "TeamName");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");

        var trainingXml = await _chpp.GetXmlAsync("training", new Dictionary<string,string?> { ["version"]="1.1" }, ct);
        var trainingTeam = XmlV5.Root(trainingXml)?.Descendants("Team").FirstOrDefault();
        var selfConfidence = XmlV5.Int(trainingTeam, "SelfConfidence");
        if (selfConfidence <= 0) selfConfidence = 4;

        var ownPlayers = await ReadPlayers(teamId, ct);
        if (ownPlayers.Count < 11) throw new InvalidOperationException("Kullanıcı takımında analiz için yeterli oyuncu verisi yok.");

        var matchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?>
        {
            ["version"]="2.2", ["teamID"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var matches = ReadMatches(matchesXml, teamId);
        var next = matches.Where(x => x.Date > DateTimeOffset.UtcNow && IsCompetitiveMatchType(x.MatchType)).OrderBy(x => x.Date).FirstOrDefault();
        if (next.MatchId <= 0) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra yaklaşan resmi maç bulunamadı.");

        var opponentId = next.HomeId == teamId ? next.AwayId : next.HomeId;
        var opponentName = next.HomeId == teamId ? next.AwayName : next.HomeName;
        if (opponentId <= 0) throw new InvalidOperationException("Rakip takım ID'si bulunamadı.");

        var opponentMatchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?>
        {
            ["version"]="2.2", ["teamID"]=opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var opponentMatches = ReadMatches(opponentMatchesXml, opponentId);
        var lastMatch = opponentMatches.Where(x => x.Date != default && x.Date <= DateTimeOffset.UtcNow && IsCompetitiveMatchType(x.MatchType)).OrderByDescending(x => x.Date).FirstOrDefault();
        if (lastMatch.MatchId <= 0) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra rakibin resmi tamamlanmış maçı bulunamadı.");

        var lineupXml = await _chpp.GetXmlAsync("matchlineup", new Dictionary<string,string?>
        {
            ["version"]="1.1", ["actionType"]="view", ["matchID"]=lastMatch.MatchId.ToString(CultureInfo.InvariantCulture), ["teamID"]=opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var lineupRoot = XmlV5.Root(lineupXml);
        var returnedMatchId = XmlV5.Int(lineupRoot, "MatchID");
        if (returnedMatchId != lastMatch.MatchId) throw new InvalidOperationException($"CHPP lineup MatchID uyuşmuyor. Beklenen {lastMatch.MatchId}, gelen {returnedMatchId}.");
        var lineupNodes = SelectFinalFieldPlayers(lineupRoot);
        if (lineupNodes.Count != 11) throw new InvalidOperationException($"Rakibin seçilen resmi son maçında final saha 11'i belirlenemedi. CHPP final saha oyuncusu: {lineupNodes.Count}.");

        var opponentHistoricalRating = await ReadDirectHistoricalOpponentRating(lastMatch.MatchId, opponentId, ct);
        var experienceLevel = Math.Clamp(XmlV5.Int(lineupRoot?.Descendants("Team").FirstOrDefault(), "ExperienceLevel"), 1, 20);
        var opponentSlots = lineupNodes.Select(p => HistoricalSlot(p, opponentHistoricalRating, experienceLevel)).ToList();
        var opponentLineup = new Lineup(
            lastMatch.HomeId == opponentId ? lastMatch.HomeName : lastMatch.AwayName,
            Formation(opponentSlots), opponentSlots);
        var opponentThreat = _threatEngine.Analyze(opponentHistoricalRating);
        var opponentProfile = new OpponentMatchProfile(opponentName, opponentLineup.Formation, opponentHistoricalRating, opponentThreat);

        // Motor 2: opponent profile is complete before our XI is selected.
        const string formation = "3-5-2";
        var ownLineup = _xiOptimization.BuildBestXI(
            teamName: teamName,
            players: ownPlayers,
            formation: formation,
            opponent: opponentProfile);

        var ownContext = new RatingContext(next.HomeId == teamId ? MatchLocation.Home : MatchLocation.Away, questionnaire.MatchImportance, TeamTactic.Normal);
        var regionalOwn = _ratingEngine.CalculateLineup(ownLineup, ownPlayers, ownContext);
        var ownRating = QuestionnaireRatingAdjuster.Apply(regionalOwn, questionnaire);
        ownRating = ConfidenceRatingAdjuster.Apply(ownRating, selfConfidence);
        var location = next.HomeId == teamId ? "Ev sahibi" : "Deplasman";
        var title = $"{next.Date.ToLocalTime():dd.MM.yyyy HH:mm} • {opponentName} • {location}";
        return new Analysis(build, teamName, opponentName, title, ownLineup, opponentLineup, ownRating, opponentHistoricalRating, questionnaire);
    }

    private static bool IsCompetitiveMatchType(int type) => type is 1 or 2 or 7 or 10 or 11;

    private static List<XElement> SelectFinalFieldPlayers(XElement? lineupRoot)
    {
        var team = lineupRoot?.Descendants("Team").FirstOrDefault();
        if (team is null) return new List<XElement>();
        var finalPlayers = team.Element("Lineup")?.Elements("Player").Where(p => XmlV5.Int(p, "PlayerID") > 0).ToList() ?? new List<XElement>();
        var startingPlayers = team.Element("StartingLineup")?.Elements("Player").Where(p => XmlV5.Int(p, "PlayerID") > 0 && XmlV5.Int(p, "RoleID") is >= 1 and <= 11).ToList() ?? new List<XElement>();
        var onField = new HashSet<int>(startingPlayers.Select(p => XmlV5.Int(p, "PlayerID")));
        foreach (var sub in team.Descendants("Substitution").Select(s => new { Subject = XmlV5.Int(s,"SubjectPlayerID"), Object = XmlV5.Int(s,"ObjectPlayerID") }).Where(x => x.Subject > 0 && x.Object > 0 && x.Subject != x.Object))
            if (onField.Remove(sub.Subject)) onField.Add(sub.Object);
        if (onField.Count == 0) onField = finalPlayers.Where(p => XmlV5.Int(p,"RoleID") is >= 1 and <= 11).Select(p => XmlV5.Int(p,"PlayerID")).Where(id => id > 0).ToHashSet();
        return finalPlayers.Where(p => onField.Contains(XmlV5.Int(p,"PlayerID")) && XmlV5.Int(p,"PositionCode") > 0).GroupBy(p => XmlV5.Int(p,"PlayerID")).Select(g => g.First()).ToList();
    }

    private async Task<RegionalRatingSnapshot> ReadDirectHistoricalOpponentRating(int matchId, int opponentId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?> { ["version"]="1.4", ["matchID"]=matchId.ToString(CultureInfo.InvariantCulture) }, ct);
        var root = XmlV5.Root(xml); var matchNode = root?.Descendants("Match").FirstOrDefault();
        if (XmlV5.Int(matchNode, "MatchID") != matchId) throw new InvalidOperationException($"CHPP matchdetails MatchID uyuşmuyor. Beklenen {matchId}.");
        var team = matchNode?.Element("HomeTeam"); if (team is null || TeamNodeId(team) != opponentId) team = matchNode?.Element("AwayTeam");
        if (team is null || TeamNodeId(team) != opponentId) throw new InvalidOperationException("Rakibin son maçındaki takım kaydı CHPP matchdetails'dan alınamadı.");
        double V(string n) => XmlV5.Int(team, n) / 4.0;
        var ld=V("RatingLeftDef"); var cd=V("RatingMidDef"); var rd=V("RatingRightDef"); var mid=V("RatingMidfield"); var la=V("RatingLeftAtt"); var ca=V("RatingMidAtt"); var ra=V("RatingRightAtt");
        return new RegionalRatingSnapshot(ld,cd,rd,mid,la,ca,ra,ld,cd,rd,mid,la,ca,ra);
    }

    private static int TeamNodeId(XElement node) => node.Name.LocalName switch { "HomeTeam" => XmlV5.Int(node,"HomeTeamID"), "AwayTeam" => XmlV5.Int(node,"AwayTeamID"), _ => 0 };

    private async Task<List<Player>> ReadPlayers(int teamId, CancellationToken ct)
    {
        var xml=await _chpp.GetXmlAsync("players",new Dictionary<string,string?> { ["version"]="1.3", ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture) },ct); var root=XmlV5.Root(xml);
        return root?.Descendants("Player").Select(p=>new Player(XmlV5.Int(p,"PlayerID"),XmlV5.Text(p,"PlayerName"),XmlV5.Int(p,"KeeperSkill"),XmlV5.Int(p,"DefenderSkill"),XmlV5.Int(p,"PlaymakerSkill"),XmlV5.Int(p,"PassingSkill"),XmlV5.Int(p,"WingerSkill"),XmlV5.Int(p,"ScorerSkill"),XmlV5.Int(p,"StaminaSkill"),XmlV5.Int(p,"PlayerForm"),XmlV5.Int(p,"Experience"),XmlV5.Int(p,"Loyalty"))).Where(p=>p.Id>0).ToList() ?? new List<Player>();
    }

    private static List<(int MatchId,DateTimeOffset Date,int HomeId,int AwayId,string HomeName,string AwayName,int MatchType)> ReadMatches(string xml,int teamId)
    {
        var result=new List<(int,DateTimeOffset,int,int,string,string,int)>();
        foreach(var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>()) { var id=XmlV5.Int(m,"MatchID");var date=XmlV5.Date(m,"MatchDate");var home=XmlV5.Int(m,"HomeTeamID");var away=XmlV5.Int(m,"AwayTeamID");var type=XmlV5.Int(m,"MatchType");if(id>0&&date!=default&&(home==teamId||away==teamId))result.Add((id,date,home,away,XmlV5.Text(m,"HomeTeamName"),XmlV5.Text(m,"AwayTeamName"),type)); }
        return result;
    }

    private static Slot HistoricalSlot(XElement node,RegionalRatingSnapshot teamRating,int experienceLevel)
    {
        var id=XmlV5.Int(node,"PlayerID");var name=XmlV5.Text(node,"PlayerName");var position=XmlV5.Int(node,"PositionCode");var behaviour=XmlV5.Int(node,"Behaviour");var stars=ParseStars(XmlV5.Text(node,"RatingStars"));
        var order=behaviour switch {1=>PlayerOrder.Offensive,2=>PlayerOrder.Defensive,3=>PlayerOrder.TowardsMiddle,4=>PlayerOrder.TowardsWing,_=>PlayerOrder.Normal};
        var map=position switch {100=>new[]{"GK","GK"},101=>new[]{"DEF-L","DEF-L"},102=>new[]{"DEF-C","DEF-C"},103=>new[]{"DEF-R","DEF-R"},104=>new[]{"W-L","W-L"},105=>new[]{"IM-L","IM-L"},106=>new[]{"IM-C","IM-C"},107=>new[]{"IM-R","IM-R"},108=>new[]{"W-R","W-R"},109=>new[]{"FW-L","FW-L"},110=>new[]{"FW-C","FW-C"},111=>new[]{"FW-R","FW-R"},_=>new[]{"UNK","UNK"}};
        return new Slot(map[0],map[1],map[1],name,id,Math.Round(Math.Clamp(stars/2.0,0,20),2),50,50,order,stars);
    }

    private static int ParseStars(string value)=>int.TryParse(value.Replace(".",string.Empty,StringComparison.Ordinal).Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var v)?v:0;
    private static string Formation(IReadOnlyList<Slot> slots){var d=slots.Count(x=>x.Code.StartsWith("DEF",StringComparison.Ordinal));var m=slots.Count(x=>x.Code.StartsWith("IM",StringComparison.Ordinal)||x.Code.StartsWith("W-",StringComparison.Ordinal));var f=slots.Count(x=>x.Code.StartsWith("FW",StringComparison.Ordinal));return $"{d}-{m}-{f}";}
}
