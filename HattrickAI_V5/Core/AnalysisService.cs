using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed class AnalysisService
{
    private readonly ChppV5 _chpp;
    private readonly RegionalRatingEngine _ratingEngine = new();
    public AnalysisService(ChppV5 chpp) => _chpp = chpp;

    public async Task<Analysis> RunAsync(string build, MatchQuestionnaire? questionnaire, CancellationToken ct)
    {
        questionnaire ??= MatchQuestionnaire.Default;

        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        var teamName = XmlV5.Text(teamNode, "TeamName");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");

        var ownPlayers = await ReadPlayers(teamId, ct);
        if (ownPlayers.Count < 11) throw new InvalidOperationException("Kullanıcı takımında analiz için yeterli oyuncu verisi yok.");

        var matchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"]="1.3", ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture) }, ct);
        var matches = ReadMatches(matchesXml, teamId);
        var next = matches.Where(x => x.Date > DateTimeOffset.UtcNow).OrderBy(x => x.Date).FirstOrDefault();
        if (next.MatchId <= 0) throw new InvalidOperationException("Yaklaşan maç bulunamadı.");

        var opponentId = next.HomeId == teamId ? next.AwayId : next.HomeId;
        var opponentName = next.HomeId == teamId ? next.AwayName : next.HomeName;
        if (opponentId <= 0) throw new InvalidOperationException("Rakip takım ID'si bulunamadı.");

        var opponentMatchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?> { ["version"]="1.3", ["teamId"]=opponentId.ToString(CultureInfo.InvariantCulture) }, ct);
        var opponentMatches = ReadMatches(opponentMatchesXml, opponentId);
        var last = opponentMatches.Where(x => x.Date < DateTimeOffset.UtcNow).OrderByDescending(x => x.Date).FirstOrDefault();
        if (last.MatchId <= 0) throw new InvalidOperationException("Rakibin son maçı bulunamadı.");

        var lineupXml = await _chpp.GetXmlAsync("matchlineup", new Dictionary<string,string?>
        {
            ["version"]="1.1",
            ["matchID"]=last.MatchId.ToString(CultureInfo.InvariantCulture),
            ["teamID"]=opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var lineupNodes = XmlV5.Root(lineupXml)?.Descendants("Player").Take(11).ToList() ?? new List<XElement>();
        if (lineupNodes.Count != 11) throw new InvalidOperationException("Rakibin son maçının 11 oyuncusu CHPP'den alınamadı.");

        var opponentPlayers = await ReadPlayers(opponentId, ct);
        var opponentById = opponentPlayers.ToDictionary(p => p.Id);
        var opponentSlots = lineupNodes.Select(p => HistoricalSlot(p, opponentById)).Where(x => x != null).Cast<Slot>().ToList();
        if (opponentSlots.Count != 11) throw new InvalidOperationException("Rakip oyuncularının tamamı güncel CHPP oyuncu verileriyle eşleşmedi.");

        var ownLineup = BuildOwnLineup(teamName, ownPlayers);
        var opponentLineup = new Lineup(opponentName, Formation(opponentSlots), opponentSlots);

        var ownContext = new RatingContext(
            next.HomeId == teamId ? MatchLocation.Home : MatchLocation.Away,
            questionnaire.MatchImportance,
            TeamTactic.Normal);

        // Rakip ratinginde tahmin/yeniden hesaplama yapma:
        // CHPP matchdetails son maçın 7 gerçek takım ratingini doğrudan verir.
        var opponentHistoricalRating = await ReadDirectHistoricalOpponentRating(last.MatchId, opponentId, ct);

        var regionalOwn = _ratingEngine.CalculateLineup(
            ownLineup,
            ownPlayers,
            ownContext);

        // The questionnaire supplies the three user-known variables that are not reliably
        // present in the basic CHPP snapshot: coach tactical style, team spirit and attitude.
        var ownRating = QuestionnaireRatingAdjuster.Apply(regionalOwn, questionnaire);
        var opponentRating = opponentHistoricalRating;

        var location = next.HomeId == teamId ? "Ev sahibi" : "Deplasman";
        var title = $"{next.Date.ToLocalTime():dd.MM.yyyy HH:mm} • {opponentName} • {location}";
        return new Analysis(build, teamName, opponentName, title, ownLineup, opponentLineup, ownRating, opponentRating);
    }

    private async Task<RegionalRatingSnapshot> ReadDirectHistoricalOpponentRating(int matchId, int opponentId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?>
        {
            ["version"]="1.4",
            ["matchID"]=matchId.ToString(CultureInfo.InvariantCulture)
        }, ct);

        var root = XmlV5.Root(xml);
        var team = root?.Descendants("HomeTeam")
            .Concat(root.Descendants("AwayTeam"))
            .FirstOrDefault(x => TeamNodeId(x) == opponentId);

        if (team is null)
            throw new InvalidOperationException("Rakibin son maçındaki gerçek bölgesel ratingleri CHPP matchdetails'dan alınamadı.");

        // Hattrick matchdetails stores these ratings in quarter-point units:
        // e.g. 43 -> 10.75, 38 -> 9.50. Use the CHPP values directly;
        // do not pass them through our player-rating engine.
        var leftDefence = XmlV5.Int(team, "RatingLeftDef") / 4.0;
        var centralDefence = XmlV5.Int(team, "RatingMidDef") / 4.0;
        var rightDefence = XmlV5.Int(team, "RatingRightDef") / 4.0;
        var midfield = XmlV5.Int(team, "RatingMidfield") / 4.0;
        var leftAttack = XmlV5.Int(team, "RatingLeftAtt") / 4.0;
        var centralAttack = XmlV5.Int(team, "RatingMidAtt") / 4.0;
        var rightAttack = XmlV5.Int(team, "RatingRightAtt") / 4.0;

        return new RegionalRatingSnapshot(
            leftDefence, centralDefence, rightDefence, midfield,
            leftAttack, centralAttack, rightAttack,
            leftDefence, centralDefence, rightDefence, midfield,
            leftAttack, centralAttack, rightAttack);
    }

    private static int TeamNodeId(XElement node)
    {
        return node.Name.LocalName switch
        {
            "HomeTeam" => XmlV5.Int(node, "HomeTeamID"),
            "AwayTeam" => XmlV5.Int(node, "AwayTeamID"),
            _ => 0
        };
    }

    private async Task<List<Player>> ReadPlayers(int teamId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("players", new Dictionary<string,string?>
        {
            ["version"]="1.3",
            ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var root = XmlV5.Root(xml);
        return root?.Descendants("Player").Select(p => new Player(
            XmlV5.Int(p,"PlayerID"), XmlV5.Text(p,"PlayerName"),
            XmlV5.Int(p,"KeeperSkill"), XmlV5.Int(p,"DefenderSkill"), XmlV5.Int(p,"PlaymakerSkill"),
            XmlV5.Int(p,"PassingSkill"), XmlV5.Int(p,"WingerSkill"), XmlV5.Int(p,"ScorerSkill"),
            XmlV5.Int(p,"StaminaSkill"), XmlV5.Int(p,"PlayerForm"), XmlV5.Int(p,"Experience"),
            XmlV5.Int(p,"Loyalty")))
            .Where(p => p.Id > 0).ToList() ?? new List<Player>();
    }

    private static List<(int MatchId,DateTimeOffset Date,int HomeId,int AwayId,string HomeName,string AwayName)> ReadMatches(string xml, int teamId)
    {
        var result = new List<(int,DateTimeOffset,int,int,string,string)>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m,"MatchID");
            var date = XmlV5.Date(m,"MatchDate");
            var home = XmlV5.Int(m,"HomeTeamID");
            var away = XmlV5.Int(m,"AwayTeamID");
            if (id > 0 && date != default && (home == teamId || away == teamId))
                result.Add((id,date,home,away,XmlV5.Text(m,"HomeTeamName"),XmlV5.Text(m,"AwayTeamName")));
        }
        return result;
    }

    private static Slot? HistoricalSlot(XElement node, Dictionary<int,Player> players)
    {
        var id = XmlV5.Int(node,"PlayerID");
        if (!players.TryGetValue(id, out var player)) return null;
        var position = XmlV5.Int(node,"PositionCode");
        var behaviour = XmlV5.Int(node,"Behaviour");
        var order = behaviour switch
        {
            1 => PlayerOrder.Offensive,
            2 => PlayerOrder.Defensive,
            3 => PlayerOrder.TowardsMiddle,
            4 => PlayerOrder.TowardsWing,
            _ => PlayerOrder.Normal
        };
        var map = position switch
        {
            1 => ("GK","Kaleci",50d,10d),
            2 => ("DEF-R","Sağ bek",88d,34d),
            3 => ("DEF-CR","Sağ stoper",70d,34d),
            4 => ("DEF-CL","Sol stoper",30d,34d),
            5 => ("DEF-L","Sol bek",12d,34d),
            6 => ("W-R","Sağ kanat",88d,50d),
            7 => ("IM-R","Sağ iç",66d,50d),
            8 => ("IM-L","Sol iç",34d,50d),
            9 => ("W-L","Sol kanat",12d,50d),
            10 => ("FW-L","Sol forvet",38d,72d),
            11 => ("FW-R","Sağ forvet",62d,72d),
            _ => ("IM-C","Merkez",50d,50d)
        };
        return MakeSlot(map.Item1,map.Item2,map.Item2,player,map.Item3,map.Item4,order);
    }

    private static Lineup BuildOwnLineup(string teamName, List<Player> players)
    {
        var unused = new HashSet<int>(players.Select(p => p.Id));
        Player Pick(Func<Player,double> score)
        {
            var p = players.Where(p => unused.Contains(p.Id)).OrderByDescending(score).FirstOrDefault();
            if (p is null) throw new InvalidOperationException("Önerilen 11 oluşturulamadı.");
            unused.Remove(p.Id);
            return p;
        }

        var gk = Pick(p => p.Keeper + p.Form*.15);
        var dl = Pick(p => p.Defending + p.Passing*.10 + p.Winger*.05);
        var dc = Pick(p => p.Defending*1.05 + p.Passing*.15 + p.Playmaking*.04);
        var dr = Pick(p => p.Defending + p.Passing*.10 + p.Winger*.05);
        var wl = Pick(p => p.Winger + p.Passing*.22 + p.Playmaking*.08);
        var il = Pick(p => p.Playmaking + p.Passing*.25 + p.Stamina*.12);
        var ic = Pick(p => p.Playmaking*1.05 + p.Passing*.25 + p.Stamina*.12 + p.Experience*.04);
        var ir = Pick(p => p.Playmaking + p.Passing*.25 + p.Stamina*.12);
        var wr = Pick(p => p.Winger + p.Passing*.22 + p.Playmaking*.08);
        var fl = Pick(p => p.Scoring + p.Passing*.18 + p.Winger*.08);
        var fr = Pick(p => p.Scoring*1.05 + p.Passing*.18 + p.Winger*.08 + p.Experience*.04);

        var slots = new List<Slot>
        {
            MakeSlot("GK","GK","Kaleci",gk,50,10),
            MakeSlot("DEF-CL","DEF-CL","Sol stoper",dl,30,34),
            MakeSlot("DEF-C","DEF-C","Merkez stoper",dc,50,34),
            MakeSlot("DEF-CR","DEF-CR","Sağ stoper",dr,70,34),
            MakeSlot("W-L","W-L","Sol kanat",wl,12,50),
            MakeSlot("IM-L","IM-L","Sol iç",il,34,50),
            MakeSlot("IM-C","IM-C","Merkez",ic,50,50),
            MakeSlot("IM-R","IM-R","Sağ iç",ir,66,50),
            MakeSlot("W-R","W-R","Sağ kanat",wr,88,50),
            MakeSlot("FW-L","FW-L","Sol forvet",fl,38,72),
            MakeSlot("FW-R","FW-R","Sağ forvet",fr,62,72)
        };
        return new Lineup(teamName,"3-5-2",slots);
    }

    private static Slot MakeSlot(string code,string label,string description,Player p,double x,double y,PlayerOrder order = PlayerOrder.Normal)
    {
        var rating = code == "GK" ? p.Keeper*.75 + p.Defending*.15 + p.Form*.10 :
            code.StartsWith("DEF") ? p.Defending*.70 + p.Passing*.15 + p.Playmaking*.10 + p.Form*.05 :
            code.StartsWith("IM") ? p.Playmaking*.55 + p.Passing*.20 + p.Stamina*.15 + p.Defending*.10 :
            code.StartsWith("W-") ? p.Winger*.45 + p.Playmaking*.20 + p.Passing*.20 + p.Defending*.10 + p.Form*.05 :
            p.Scoring*.55 + p.Passing*.20 + p.Winger*.15 + p.Playmaking*.10;
        return new Slot(code,label,description,p.Name,p.Id,Math.Round(Math.Clamp(rating,0,20),2),x,y,order);
    }

    private static string Formation(IReadOnlyList<Slot> slots)
    {
        var d = slots.Count(x => x.Code.StartsWith("DEF",StringComparison.Ordinal));
        var m = slots.Count(x => x.Code.StartsWith("IM",StringComparison.Ordinal) || x.Code.StartsWith("W-",StringComparison.Ordinal));
        var f = slots.Count(x => x.Code.StartsWith("FW",StringComparison.Ordinal));
        return $"{d}-{m}-{f}";
    }
}
