using System.Globalization;
using System.Xml.Linq;
using HattrickAI.HOEngine;

namespace HattrickAI.CHPP;

public sealed record ChppFixture(
    int MatchId,
    DateTime MatchDate,
    int MatchType,
    string Status,
    int HomeTeamId,
    string HomeTeamName,
    int AwayTeamId,
    string AwayTeamName,
    int? HomeGoals,
    int? AwayGoals)
{
    public bool IsOwnHome(int ownTeamId) => HomeTeamId == ownTeamId;
    public int OpponentTeamId(int ownTeamId) => IsOwnHome(ownTeamId) ? AwayTeamId : HomeTeamId;
    public string OpponentName(int ownTeamId) => IsOwnHome(ownTeamId) ? AwayTeamName : HomeTeamName;
    public string VenueText(int ownTeamId) => IsOwnHome(ownTeamId) ? "Ev sahibi" : "Deplasman";

    public string ScoreText => HomeGoals.HasValue && AwayGoals.HasValue
        ? $"{HomeGoals}-{AwayGoals}"
        : "—";
}

public sealed record ChppOpponentMatch(
    ChppFixture Fixture,
    TeamData OpponentTeam,
    TeamData OtherTeam);

public sealed record ChppSelectedMatch(
    ChppFixture Fixture,
    int OpponentTeamId,
    string OpponentTeamName,
    TeamData OpponentRatings,
    IReadOnlyList<ChppOpponentMatch> RecentMatches);

/// <summary>
/// Own fixture list + selected opponent history. The matches endpoint is
/// documented as current version 2.2 and normally returns recent/upcoming
/// matches. Match details version 1.4 supplies the seven sector ratings.
/// </summary>
public sealed class ChppMatchDataService
{
    private readonly ChppOAuthClient _oauth;

    public ChppMatchDataService(ChppOAuthClient oauth)
    {
        _oauth = oauth;
    }

    public async Task<IReadOnlyList<ChppFixture>> LoadUpcomingFixturesAsync(
        int ownTeamId,
        CancellationToken cancellationToken = default)
    {
        var xml = await _oauth.GetXmlAsync(
            "matches",
            new Dictionary<string, string?>
            {
                ["version"] = "2.2",
                ["teamID"] = ownTeamId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        return ParseMatches(xml)
            .Where(m => m.Status.Equals("UPCOMING", StringComparison.OrdinalIgnoreCase)
                     || m.Status.Equals("ONGOING", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate >= DateTime.Now.AddMinutes(-10))
            .OrderBy(m => m.MatchDate)
            .ToList();
    }

    public async Task<ChppSelectedMatch> LoadSelectedMatchAsync(
        ChppFixture fixture,
        int ownTeamId,
        CancellationToken cancellationToken = default)
    {
        var opponentId = fixture.OpponentTeamId(ownTeamId);
        var opponentName = fixture.OpponentName(ownTeamId);

        if (opponentId <= 0)
            throw new InvalidDataException("Seçilen maçın rakip takım ID'si okunamadı.");

        var opponentMatchesXml = await _oauth.GetXmlAsync(
            "matches",
            new Dictionary<string, string?>
            {
                ["version"] = "2.2",
                ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        var recentFixtures = ParseMatches(opponentMatchesXml)
            .Where(m => m.Status.Equals("FINISHED", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate < DateTime.Now.AddMinutes(5))
            .OrderByDescending(m => m.MatchDate)
            .Take(5)
            .ToList();

        if (recentFixtures.Count == 0)
            throw new InvalidDataException($"{opponentName} için tamamlanmış son maç bulunamadı.");

        var detailed = new List<ChppOpponentMatch>();
        foreach (var recent in recentFixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var xml = await _oauth.GetXmlAsync(
                "matchdetails",
                new Dictionary<string, string?>
                {
                    ["version"] = "1.4",
                    ["matchID"] = recent.MatchId.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            var parsed = ParseMatchDetails(xml, recent);
            var opponentTeam = parsed.HomeTeam.TeamId == opponentId
                ? parsed.HomeTeam.Data
                : parsed.AwayTeam.Data;
            var otherTeam = parsed.HomeTeam.TeamId == opponentId
                ? parsed.AwayTeam.Data
                : parsed.HomeTeam.Data;

            detailed.Add(new ChppOpponentMatch(recent, opponentTeam, otherTeam));
        }

        var average = AverageTeamData(opponentName, detailed.Select(x => x.OpponentTeam));

        return new ChppSelectedMatch(
            fixture,
            opponentId,
            opponentName,
            average,
            detailed);
    }

    private static IReadOnlyList<ChppFixture> ParseMatches(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new List<ChppFixture>();

        foreach (var match in doc.Descendants("Match"))
        {
            var matchId = ReadInt(match, "MatchID");
            var date = ReadDate(match, "MatchDate");
            var home = match.Element("HomeTeam");
            var away = match.Element("AwayTeam");

            if (matchId <= 0 || date == DateTime.MinValue || home == null || away == null)
                continue;

            result.Add(new ChppFixture(
                matchId,
                date,
                ReadInt(match, "MatchType"),
                ReadText(match, "Status") ?? string.Empty,
                ReadInt(home, "HomeTeamID"),
                ReadText(home, "HomeTeamName") ?? "Ev sahibi",
                ReadInt(away, "AwayTeamID"),
                ReadText(away, "AwayTeamName") ?? "Deplasman",
                ReadNullableInt(home, "HomeGoals"),
                ReadNullableInt(away, "AwayGoals")));
        }

        return result;
    }

    private static ParsedMatch ParseMatchDetails(string xml, ChppFixture fallback)
    {
        var doc = XDocument.Parse(xml);
        var match = doc.Descendants("Match").FirstOrDefault()
            ?? throw new InvalidDataException("CHPP matchdetails XML içinde Match bulunamadı.");

        var home = match.Element("HomeTeam");
        var away = match.Element("AwayTeam");
        if (home == null || away == null)
            throw new InvalidDataException("CHPP matchdetails XML takım bilgilerini içermiyor.");

        return new ParsedMatch(
            ReadInt(match, "MatchID", fallback.MatchId),
            ReadDate(match, "MatchDate", fallback.MatchDate),
            ParseTeam(home, "HomeTeamID", "HomeTeamName"),
            ParseTeam(away, "AwayTeamID", "AwayTeamName"));
    }

    private static ParsedTeam ParseTeam(XElement node, string idName, string nameName)
    {
        // CHPP matchDetails ratings are on the official 1..80 scale.
        // Our HO-style lineup engine works on the 0..20-ish team-rating scale,
        // so the match values must be normalized before they enter comparisons
        // and the simulator. Feeding raw 40-60 values here makes every matchup
        // look absurdly one-sided.
        double Rating20(string elementName) =>
            Math.Clamp(ReadDouble(node, elementName) / 4.0, 0.0, 20.0);

        var data = new TeamData(
            ReadText(node, nameName) ?? "Bilinmeyen Takım",
            new TeamRatings(
                Rating20("RatingMidfield"),
                Rating20("RatingLeftDef"),
                Rating20("RatingMidDef"),
                Rating20("RatingRightDef"),
                Rating20("RatingLeftAtt"),
                Rating20("RatingMidAtt"),
                Rating20("RatingRightAtt")),
            ReadInt(node, "TacticType"),
            ReadInt(node, "TacticSkill"));

        return new ParsedTeam(ReadInt(node, idName), data);
    }

    private static TeamData AverageTeamData(string name, IEnumerable<TeamData> teams)
    {
        var list = teams.ToList();
        if (list.Count == 0)
            throw new InvalidDataException("Rakip geçmiş maçlarından rating üretilemedi.");

        var ratings = new TeamRatings(
            list.Average(t => t.Ratings.Midfield),
            list.Average(t => t.Ratings.LeftDefence),
            list.Average(t => t.Ratings.CentralDefence),
            list.Average(t => t.Ratings.RightDefence),
            list.Average(t => t.Ratings.LeftAttack),
            list.Average(t => t.Ratings.CentralAttack),
            list.Average(t => t.Ratings.RightAttack));

        var tactic = list
            .GroupBy(t => t.TacticType)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(x => x.TacticLevel))
            .First().Key;

        var level = (int)Math.Round(list.Average(t => t.TacticLevel), MidpointRounding.AwayFromZero);
        return new TeamData(name, ratings, tactic, level);
    }

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = ReadText(parent, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static int? ReadNullableInt(XElement parent, string name)
    {
        var text = ReadText(parent, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double ReadDouble(XElement parent, string name, double fallback = 0)
    {
        var text = ReadText(parent, name);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static DateTime ReadDate(XElement parent, string name, DateTime fallback = default)
    {
        var text = ReadText(parent, name);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value
            : fallback;
    }

    private static string? ReadText(XElement parent, string name) =>
        parent.Element(name)?.Value?.Trim();

    private sealed record ParsedTeam(int TeamId, TeamData Data);
    private sealed record ParsedMatch(int MatchId, DateTime MatchDate, ParsedTeam HomeTeam, ParsedTeam AwayTeam);
}
