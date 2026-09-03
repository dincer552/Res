using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace HattrickAI.V5.Core;

public sealed class AnalysisService
{
    private readonly ChppV5 _chpp;
    private readonly IHttpContextAccessor _http;
    private readonly OpponentThreatEngine _threatEngine = new();
    private readonly MotorPipelineService _motors = new();

    public AnalysisService(ChppV5 chpp, IHttpContextAccessor http)
    {
        _chpp = chpp;
        _http = http;
    }

    public async Task<Analysis> RunAsync(string build, MatchQuestionnaire? questionnaire, CancellationToken ct)
    {
        questionnaire ??= MatchQuestionnaire.Default;

        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string, string?> { ["version"] = "3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        var teamName = XmlV5.Text(teamNode, "TeamName");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");

        var trainingXml = await _chpp.GetXmlAsync("training", new Dictionary<string, string?> { ["version"] = "1.1" }, ct);
        var trainingTeam = XmlV5.Root(trainingXml)?.Descendants("Team").FirstOrDefault();
        var selfConfidence = Math.Max(1, XmlV5.Int(trainingTeam, "SelfConfidence"));

        var ownPlayers = await ReadPlayers(teamId, ct);
        if (ownPlayers.Count < 11)
            throw new InvalidOperationException("Kullanıcı takımında analiz için yeterli oyuncu verisi yok.");

        var matchesXml = await _chpp.GetXmlAsync(
            "matches",
            new Dictionary<string, string?> { ["version"] = "2.2", ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture) },
            ct);
        var matches = ReadMatches(matchesXml, teamId);
        var now = DateTimeOffset.UtcNow;

        var selectedText = _http.HttpContext?.Request.Cookies["v5.matchId"];
        if (!int.TryParse(selectedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectedMatchId) || selectedMatchId <= 0)
            throw new InvalidOperationException("Önce analiz edilecek lig maçını seçmelisin.");

        var next = matches.FirstOrDefault(x => x.MatchId == selectedMatchId && x.Date > now && x.MatchType == 1);
        if (next.MatchId <= 0)
            throw new InvalidOperationException("Seçilen maç geçersiz, geçmişte kalmış veya lig maçı değil. Lütfen yaklaşan lig maçlarından birini seç.");

        var opponentId = next.HomeId == teamId ? next.AwayId : next.HomeId;
        var opponentName = next.HomeId == teamId ? next.AwayName : next.HomeName;
        if (opponentId <= 0) throw new InvalidOperationException("Rakip takım ID'si bulunamadı.");

        var opponentMatchesXml = await _chpp.GetXmlAsync(
            "matches",
            new Dictionary<string, string?> { ["version"] = "2.2", ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture) },
            ct);
        var opponentMatches = ReadMatches(opponentMatchesXml, opponentId);
        var lastMatch = opponentMatches
            .Where(x => x.Date != default && x.Date <= now && IsCompetitiveMatchType(x.MatchType))
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();

        if (lastMatch.MatchId <= 0)
            throw new InvalidOperationException("Rakibin resmi tamamlanmış maçı bulunamadı.");

        var lineupXml = await _chpp.GetXmlAsync(
            "matchlineup",
            new Dictionary<string, string?>
            {
                ["version"] = "1.1",
                ["actionType"] = "view",
                ["matchID"] = lastMatch.MatchId.ToString(CultureInfo.InvariantCulture),
                ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture)
            },
            ct);
        var lineupRoot = XmlV5.Root(lineupXml);
        if (XmlV5.Int(lineupRoot, "MatchID") != lastMatch.MatchId)
            throw new InvalidOperationException("CHPP lineup MatchID uyuşmuyor.");

        var lineupNodes = SelectFinalFieldPlayers(lineupRoot);
        if (lineupNodes.Count != 11)
            throw new InvalidOperationException($"Rakibin son resmi maçında final saha 11'i belirlenemedi: {lineupNodes.Count}.");

        var opponentHistoricalRating = await ReadDirectHistoricalOpponentRating(lastMatch.MatchId, opponentId, ct);
        var experienceLevel = Math.Clamp(XmlV5.Int(lineupRoot?.Descendants("Team").FirstOrDefault(), "ExperienceLevel"), 1, 20);
        var opponentSlots = lineupNodes.Select(p => HistoricalSlot(p, opponentHistoricalRating, experienceLevel)).ToList();
        var opponentLineup = Formation(opponentName, opponentSlots);
        var opponentThreat = _threatEngine.Analyze(opponentHistoricalRating);
        var opponentProfile = new OpponentMatchProfile(opponentName, opponentLineup.Formation, opponentHistoricalRating, opponentThreat);

        var locationEnum = next.HomeId == teamId ? MatchLocation.Home : MatchLocation.Away;
        var ratingContext = new RatingContext(locationEnum, questionnaire.MatchImportance, TeamTactic.Normal);
        var context = new MatchDataContext(ownPlayers, teamId, teamName, opponentProfile, ratingContext, questionnaire);

        var pipeline = await _motors.RunAsync(context, ownPlayers, ct);
        var finalLineup = pipeline.FinalPlan.Lineup;
        var finalRating = ConfidenceRatingAdjuster.Apply(pipeline.FinalPlan.Rating, selfConfidence);
        var appliedQuestionnaire = questionnaire with { MatchImportance = pipeline.SelectedMatchApproach };

        var location = next.HomeId == teamId ? "Ev sahibi" : "Deplasman";
        var title = $"{next.Date.ToLocalTime():dd.MM.yyyy HH:mm} • {opponentName} • {location}";

        return new Analysis(
            build,
            teamName,
            opponentName,
            title,
            finalLineup,
            opponentLineup,
            finalRating,
            opponentHistoricalRating,
            appliedQuestionnaire)
        {
            M7Scenario = pipeline.M7,
            M72Scenario = pipeline.M72,
            M8Chance = pipeline.M8,
            M9Prediction = pipeline.M9,
            M10Decision = pipeline.M10,
            MotorPipeline = pipeline
        };
    }

    private static bool IsCompetitiveMatchType(int type) => type is 1 or 2 or 7 or 10 or 11;

    private static List<XElement> SelectFinalFieldPlayers(XElement? root)
    {
        var team = root?.Descendants("Team").FirstOrDefault();
        if (team is null) return new();
        var finalPlayers = team.Element("Lineup")?.Elements("Player").Where(p => XmlV5.Int(p, "PlayerID") > 0).ToList() ?? new();
        var starting = team.Element("StartingLineup")?.Elements("Player").Where(p => XmlV5.Int(p, "PlayerID") > 0 && XmlV5.Int(p, "RoleID") is >= 1 and <= 11).ToList() ?? new();
        var onField = starting.Select(p => XmlV5.Int(p, "PlayerID")).ToHashSet();
        foreach (var s in team.Descendants("Substitution"))
        {
            var subject = XmlV5.Int(s, "SubjectPlayerID");
            var obj = XmlV5.Int(s, "ObjectPlayerID");
            if (subject > 0 && obj > 0 && onField.Remove(subject)) onField.Add(obj);
        }
        if (onField.Count == 0)
            onField = finalPlayers.Where(p => XmlV5.Int(p, "RoleID") is >= 1 and <= 11).Select(p => XmlV5.Int(p, "PlayerID")).ToHashSet();
        return finalPlayers.Where(p => onField.Contains(XmlV5.Int(p, "PlayerID")) && XmlV5.Int(p, "PositionCode") > 0).GroupBy(p => XmlV5.Int(p, "PlayerID")).Select(g => g.First()).ToList();
    }

    private async Task<RegionalRatingSnapshot> ReadDirectHistoricalOpponentRating(int matchId, int opponentId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("matchdetails", new Dictionary<string, string?> { ["version"] = "1.4", ["matchID"] = matchId.ToString(CultureInfo.InvariantCulture) }, ct);
        var match = XmlV5.Root(xml)?.Descendants("Match").FirstOrDefault();
        if (XmlV5.Int(match, "MatchID") != matchId) throw new InvalidOperationException("CHPP matchdetails MatchID uyuşmuyor.");
        var team = match?.Element("HomeTeam");
        if (team is null || TeamNodeId(team) != opponentId) team = match?.Element("AwayTeam");
        if (team is null || TeamNodeId(team) != opponentId) throw new InvalidOperationException("Rakibin matchdetails kaydı alınamadı.");
        return new RegionalRatingSnapshot(
            XmlV5.Int(team, "RatingLeftDef") / 4.0, XmlV5.Int(team, "RatingMidDef") / 4.0, XmlV5.Int(team, "RatingRightDef") / 4.0,
            XmlV5.Int(team, "RatingMidfield") / 4.0, XmlV5.Int(team, "RatingLeftAtt") / 4.0, XmlV5.Int(team, "RatingMidAtt") / 4.0, XmlV5.Int(team, "RatingRightAtt") / 4.0,
            XmlV5.Int(team, "RatingLeftDef") / 4.0, XmlV5.Int(team, "RatingMidDef") / 4.0, XmlV5.Int(team, "RatingRightDef") / 4.0,
            XmlV5.Int(team, "RatingMidfield") / 4.0, XmlV5.Int(team, "RatingLeftAtt") / 4.0, XmlV5.Int(team, "RatingMidAtt") / 4.0, XmlV5.Int(team, "RatingRightAtt") / 4.0);
    }

    private static int TeamNodeId(XElement node) => node.Name.LocalName switch
    {
        "HomeTeam" => XmlV5.Int(node, "HomeTeamID"),
        "AwayTeam" => XmlV5.Int(node, "AwayTeamID"),
        _ => 0
    };

    private async Task<List<Player>> ReadPlayers(int teamId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("players", new Dictionary<string, string?> { ["version"] = "1.3", ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture) }, ct);
        return XmlV5.Root(xml)?.Descendants("Player").Select(p => new Player(
            XmlV5.Int(p, "PlayerID"), XmlV5.Text(p, "PlayerName"), XmlV5.Int(p, "KeeperSkill"), XmlV5.Int(p, "DefenderSkill"), XmlV5.Int(p, "PlaymakerSkill"), XmlV5.Int(p, "PassingSkill"), XmlV5.Int(p, "WingerSkill"), XmlV5.Int(p, "ScorerSkill"), XmlV5.Int(p, "StaminaSkill"), XmlV5.Int(p, "PlayerForm"), XmlV5.Int(p, "Experience"), XmlV5.Int(p, "Loyalty"), XmlV5.Int(p, "InjuryLevel"), ParseSpecialty(p)))
            .Where(p => p.Id > 0).ToList() ?? new();
    }

    private static PlayerSpecialty ParseSpecialty(XElement player)
    {
        var value = XmlV5.Int(player, "Specialty");
        return Enum.IsDefined(typeof(PlayerSpecialty), value)
            ? (PlayerSpecialty)value
            : PlayerSpecialty.None;
    }

    private static List<(int MatchId, DateTimeOffset Date, int HomeId, int AwayId, string HomeName, string AwayName, int MatchType)> ReadMatches(string xml, int teamId)
    {
        var result = new List<(int, DateTimeOffset, int, int, string, string, int)>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m, "MatchID"); var date = XmlV5.Date(m, "MatchDate"); var home = XmlV5.Int(m, "HomeTeamID"); var away = XmlV5.Int(m, "AwayTeamID"); var type = XmlV5.Int(m, "MatchType");
            if (id > 0 && date != default && (home == teamId || away == teamId)) result.Add((id, date, home, away, XmlV5.Text(m, "HomeTeamName"), XmlV5.Text(m, "AwayTeamName"), type));
        }
        return result;
    }

    private static Slot HistoricalSlot(XElement node, RegionalRatingSnapshot rating, int experienceLevel)
    {
        var id = XmlV5.Int(node, "PlayerID"); var name = XmlV5.Text(node, "PlayerName"); var position = XmlV5.Int(node, "PositionCode"); var behaviour = XmlV5.Int(node, "Behaviour"); var stars = ParseStars(XmlV5.Text(node, "RatingStars"));
        var order = behaviour switch { 1 => PlayerOrder.Offensive, 2 => PlayerOrder.Defensive, 3 => PlayerOrder.TowardsMiddle, 4 => PlayerOrder.TowardsWing, _ => PlayerOrder.Normal };
        var map = behaviour switch
        {
            7 => ("DEF-C", "Merkez stoper", 50d, 34d), 6 => ("IM-C", "Merkez iç", 50d, 50d), 5 => ("FW-C", "Merkez forvet", 50d, 72d),
            _ => position switch
            {
                1 => ("GK", "Kaleci", 50d, 10d), 2 => ("DEF-R", "Sağ bek", 88d, 34d), 3 => ("DEF-CR", "Sağ stoper", 70d, 34d), 4 => ("DEF-CL", "Sol stoper", 30d, 34d), 5 => ("DEF-L", "Sol bek", 12d, 34d),
                6 => ("W-R", "Sağ kanat", 88d, 50d), 7 => ("IM-R", "Sağ iç", 66d, 50d), 8 => ("IM-L", "Sol iç", 34d, 50d), 9 => ("W-L", "Sol kanat", 12d, 50d), 10 => ("FW-R", "Sağ forvet", 62d, 72d), 11 => ("FW-L", "Sol forvet", 38d, 72d), _ => ("IM-C", "Merkez", 50d, 50d)
            }
        };
        var rp = OpponentRatingEstimator.Estimate(stars, map.Item1, behaviour, rating, experienceLevel);
        return new Slot(map.Item1, map.Item2, map.Item2, name, id, rp, map.Item3, map.Item4, order, stars);
    }

    private static double ParseStars(string value) => double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, 0, 10) : 0;

    private static Lineup Formation(string teamName, IReadOnlyList<Slot> slots)
    {
        var d = slots.Count(x => x.Code.StartsWith("DEF", StringComparison.Ordinal));
        var m = slots.Count(x => x.Code.StartsWith("IM", StringComparison.Ordinal) || x.Code.StartsWith("W-", StringComparison.Ordinal));
        var f = slots.Count(x => x.Code.StartsWith("FW", StringComparison.Ordinal));
        return new Lineup(teamName, $"{d}-{m}-{f}", slots);
    }
}
