using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed class RatingTestService
{
    private readonly ChppV5 _chpp;
    public RatingTestService(ChppV5 chpp) => _chpp = chpp;

    public async Task<RatingTestResult> RunAsync(CancellationToken ct)
    {
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string,string?> { ["version"]="3.0" }, ct);
        var team = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(team, "TeamID");
        var teamName = XmlV5.Text(team, "TeamName");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");

        var matchesXml = await _chpp.GetXmlAsync("matches", new Dictionary<string,string?>
        {
            ["version"]="1.3", ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var matches = ReadMatches(matchesXml, teamId);
        var match = matches.Where(x => x.Date < DateTimeOffset.UtcNow && x.MatchId > 0 && x.HomeGoals.HasValue && x.AwayGoals.HasValue)
                           .OrderByDescending(x => x.Date).FirstOrDefault();
        if (match.MatchId <= 0) throw new InvalidOperationException("Test için tamamlanmış maç bulunamadı.");

        var detailsXml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string,string?>
        {
            ["version"]="1.4", ["matchID"]=match.MatchId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var details = ParseMatchDetails(detailsXml, teamId);

        var lineupXml = await _chpp.GetXmlAsync("matchlineup", new Dictionary<string,string?>
        {
            ["version"]="1.1", ["matchID"]=match.MatchId.ToString(CultureInfo.InvariantCulture), ["teamID"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var lineup = ParseLineup(lineupXml);

        var playersXml = await _chpp.GetXmlAsync("players", new Dictionary<string,string?>
        {
            ["version"]="1.3", ["teamId"]=teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var players = ParsePlayers(playersXml).ToDictionary(x => x.Id);
        var inputs = lineup.Select(x => ToRegionalPlayer(x, players)).Where(x => x != null).Cast<RegionalPlayer>().ToList();

        var context = new RatingContext(
            details.IsHome ? MatchLocation.Home : MatchLocation.Away,
            details.Attitude switch { 1 => TeamAttitude.MatchOfTheSeason, -1 => TeamAttitude.PlayItCool, _ => TeamAttitude.Normal },
            details.TacticType switch { 2 => TeamTactic.CounterAttack, 0 => TeamTactic.Normal, _ => TeamTactic.Normal });

        var calculated = new RegionalRatingEngine().Calculate(inputs, context);
        var actual = details.Actual ?? throw new InvalidOperationException("Maç detaylarında takım ratingleri bulunamadı.");
        return new RatingTestResult(teamName, match, actual, calculated, lineup.Count, inputs.Count);
    }

    private static List<(int MatchId, DateTimeOffset Date, int HomeId, int AwayId, string HomeName, string AwayName, int? HomeGoals, int? AwayGoals)> ReadMatches(string xml, int teamId)
    {
        var result = new List<(int,DateTimeOffset,int,int,string,string,int?,int?)>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m,"MatchID"); var date = XmlV5.Date(m,"MatchDate");
            var h = XmlV5.Int(m,"HomeTeamID"); var a = XmlV5.Int(m,"AwayTeamID");
            if (id <= 0 || date == default || (h != teamId && a != teamId)) continue;
            result.Add((id,date,h,a,XmlV5.Text(m,"HomeTeamName"),XmlV5.Text(m,"AwayTeamName"),NullableInt(m,"HomeGoals"),NullableInt(m,"AwayGoals")));
        }
        return result;
    }

    private static MatchActuals ParseMatchDetails(string xml, int teamId)
    {
        var match = XmlV5.Root(xml)?.Descendants("Match").FirstOrDefault() ?? throw new InvalidOperationException("matchdetails sonucu boş.");
        var home = match.Element("HomeTeam"); var away = match.Element("AwayTeam");
        var homeId = XmlV5.Int(home,"HomeTeamID"); var isHome = homeId == teamId;
        var node = isHome ? home : away;
        return new MatchActuals(
            isHome,
            XmlV5.Int(node,"TeamAttitude"),
            XmlV5.Int(node,"TacticType"),
            new ActualRating(
                XmlV5.Int(node,"RatingLeftDef"), XmlV5.Int(node,"RatingMidDef"), XmlV5.Int(node,"RatingRightDef"),
                XmlV5.Int(node,"RatingMidfield"), XmlV5.Int(node,"RatingLeftAtt"), XmlV5.Int(node,"RatingMidAtt"), XmlV5.Int(node,"RatingRightAtt")));
    }

    private static List<LineupInput> ParseLineup(string xml)
    {
        return (XmlV5.Root(xml)?.Descendants("Player") ?? Enumerable.Empty<XElement>())
            .Where(x => XmlV5.Int(x,"PositionCode") is >= 1 and <= 11)
            .Take(11)
            .Select(x => new LineupInput(XmlV5.Int(x,"PlayerID"), XmlV5.Int(x,"PositionCode"), XmlV5.Int(x,"Behaviour")))
            .ToList();
    }

    private static List<Player> ParsePlayers(string xml)
    {
        return (XmlV5.Root(xml)?.Descendants("Player") ?? Enumerable.Empty<XElement>())
            .Select(p => new Player(XmlV5.Int(p,"PlayerID"), XmlV5.Text(p,"PlayerName"), XmlV5.Int(p,"KeeperSkill"), XmlV5.Int(p,"DefenderSkill"), XmlV5.Int(p,"PlaymakerSkill"), XmlV5.Int(p,"PassingSkill"), XmlV5.Int(p,"WingerSkill"), XmlV5.Int(p,"ScorerSkill"), XmlV5.Int(p,"StaminaSkill"), XmlV5.Int(p,"PlayerForm"), XmlV5.Int(p,"Experience")))
            .Where(p => p.Id > 0).ToList();
    }

    private static RegionalPlayer? ToRegionalPlayer(LineupInput input, Dictionary<int,Player> players)
    {
        if (!players.TryGetValue(input.PlayerId, out var p)) return null;
        var position = input.PositionCode switch { 1 => RegionalPosition.Goalkeeper, 2 or 5 => RegionalPosition.WingBack, 3 or 4 => RegionalPosition.CentralDefender, 6 or 9 => RegionalPosition.Winger, 7 or 8 => RegionalPosition.InnerMidfielder, 10 or 11 => RegionalPosition.Forward, _ => RegionalPosition.Forward };
        var side = input.PositionCode switch { 5 or 9 => PlayerSide.Left, 2 or 6 => PlayerSide.Right, _ => PlayerSide.Center };
        var order = input.Behaviour switch { 1 => PlayerOrder.Offensive, 2 => PlayerOrder.Defensive, 3 => PlayerOrder.TowardsMiddle, 4 => PlayerOrder.TowardsWing, _ => PlayerOrder.Normal };
        return new RegionalPlayer(p.Id, position, side, order, p.Keeper, p.Defending, p.Playmaking, p.Passing, p.Winger, p.Scoring, p.Form, 0, p.Experience);
    }

    private static int? NullableInt(XElement? e,string name)
    { var t=XmlV5.Text(e,name); return int.TryParse(t,NumberStyles.Integer,CultureInfo.InvariantCulture,out var v)?v:null; }
}

public sealed record RatingTestResult(string TeamName, (int MatchId, DateTimeOffset Date, int HomeId, int AwayId, string HomeName, string AwayName, int? HomeGoals, int? AwayGoals) Match, ActualRating Actual, RegionalRatingSnapshot Calculated, int LineupPlayers, int PlayersMatched);
public sealed record ActualRating(int LeftDefence,int CentralDefence,int RightDefence,int Midfield,int LeftAttack,int CentralAttack,int RightAttack);
public sealed record MatchActuals(bool IsHome,int Attitude,int TacticType,ActualRating? Actual);
internal sealed record LineupInput(int PlayerId,int PositionCode,int Behaviour);