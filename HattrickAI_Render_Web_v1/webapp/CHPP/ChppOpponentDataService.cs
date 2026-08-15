using System.Globalization;
using System.Xml.Linq;
using HattrickAI.HOEngine;

namespace HattrickAI.CHPP;

public sealed record ChppOpponentSnapshot(
    int OwnTeamId,
    string OwnTeamName,
    int UpcomingMatchId,
    DateTime UpcomingMatchDate,
    int OpponentTeamId,
    string OpponentTeamName,
    int RatingMatchId,
    DateTime RatingMatchDate,
    TeamData Opponent);

/// <summary>
/// Loads the next opponent from CHPP and automatically uses that opponent's
/// most recent finished match for rating analysis. No HTML file is required.
/// </summary>
public sealed class ChppOpponentDataService
{
    private readonly ChppOAuthClient _oauth;

    public ChppOpponentDataService(ChppOAuthClient oauth)
    {
        _oauth = oauth;
    }

    public async Task<ChppOpponentSnapshot> LoadUpcomingOpponentAsync(
        int ownTeamId,
        string ownTeamName,
        CancellationToken cancellationToken = default)
    {
        var ownMatchesXml = await _oauth.GetXmlAsync(
            "matches",
            new Dictionary<string, string?>
            {
                ["version"] = "2.2",
                ["teamId"] = ownTeamId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        var upcoming = ParseMatches(ownMatchesXml)
            .Where(m => m.Status.Equals("UPCOMING", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate > DateTime.Now.AddMinutes(-5))
            .OrderBy(m => m.MatchDate)
            .FirstOrDefault();

        if (upcoming == null)
            throw new InvalidDataException("CHPP'de yaklaşan maç bulunamadı.");

        var opponent = upcoming.HomeTeamId == ownTeamId
            ? new MatchTeam(upcoming.AwayTeamId, upcoming.AwayTeamName)
            : new MatchTeam(upcoming.HomeTeamId, upcoming.HomeTeamName);

        if (opponent.TeamId <= 0)
            throw new InvalidDataException("Yaklaşan maçın rakip takım bilgisi okunamadı.");

        var opponentMatchesXml = await _oauth.GetXmlAsync(
            "matches",
            new Dictionary<string, string?>
            {
                ["version"] = "2.2",
                ["teamId"] = opponent.TeamId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        var latestFinished = ParseMatches(opponentMatchesXml)
            .Where(m => m.Status.Equals("FINISHED", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate < DateTime.Now.AddMinutes(5))
            .OrderByDescending(m => m.MatchDate)
            .FirstOrDefault();

        if (latestFinished == null)
            throw new InvalidDataException(
                $"{opponent.TeamName} için tamamlanmış son maç bulunamadı.");

        var detailsXml = await _oauth.GetXmlAsync(
            "matchdetails",
            new Dictionary<string, string?>
            {
                ["version"] = "1.4",
                ["matchID"] = latestFinished.MatchId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        var details = ParseMatchDetails(detailsXml, latestFinished.MatchId);
        var opponentTeam = details.HomeTeam.TeamId == opponent.TeamId
            ? details.HomeTeam.Data
            : details.AwayTeam.Data;

        return new ChppOpponentSnapshot(
            ownTeamId,
            ownTeamName,
            upcoming.MatchId,
            upcoming.MatchDate,
            opponent.TeamId,
            opponent.TeamName,
            details.MatchId,
            details.MatchDate,
            opponentTeam);
    }

    private static List<MatchSummary> ParseMatches(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new List<MatchSummary>();

        foreach (var match in doc.Descendants("Match"))
        {
            var matchId = ReadInt(match, "MatchID");
            var matchDate = ReadDate(match, "MatchDate");
            var home = match.Element("HomeTeam");
            var away = match.Element("AwayTeam");

            if (matchId <= 0 || matchDate == DateTime.MinValue || home == null || away == null)
                continue;

            result.Add(new MatchSummary(
                matchId,
                matchDate,
                ReadText(match, "Status") ?? string.Empty,
                ReadInt(home, "HomeTeamID"),
                ReadText(home, "HomeTeamName") ?? "Ev sahibi",
                ReadInt(away, "AwayTeamID"),
                ReadText(away, "AwayTeamName") ?? "Deplasman"));
        }

        return result;
    }

    private static ParsedMatchDetails ParseMatchDetails(string xml, int fallbackMatchId)
    {
        var doc = XDocument.Parse(xml);
        var match = doc.Descendants("Match").FirstOrDefault();
        if (match == null)
            throw new InvalidDataException("CHPP matchdetails XML içinde Match bulunamadı.");

        var home = match.Element("HomeTeam");
        var away = match.Element("AwayTeam");
        if (home == null || away == null)
            throw new InvalidDataException("CHPP matchdetails XML içinde takım bilgileri bulunamadı.");

        var matchId = ReadInt(match, "MatchID", fallbackMatchId);
        var matchDate = ReadDate(match, "MatchDate");

        return new ParsedMatchDetails(
            matchId,
            matchDate,
            ParseTeam(home, "HomeTeamID", "HomeTeamName"),
            ParseTeam(away, "AwayTeamID", "AwayTeamName"));
    }

    private static ParsedTeam ParseTeam(
        XElement node,
        string idName,
        string nameName)
    {
        var data = new TeamData(
            ReadText(node, nameName) ?? "Bilinmeyen Takım",
            new TeamRatings(
                ReadDouble(node, "RatingMidfield"),
                ReadDouble(node, "RatingLeftDef"),
                ReadDouble(node, "RatingMidDef"),
                ReadDouble(node, "RatingRightDef"),
                ReadDouble(node, "RatingLeftAtt"),
                ReadDouble(node, "RatingMidAtt"),
                ReadDouble(node, "RatingRightAtt")),
            ReadInt(node, "TacticType"),
            ReadInt(node, "TacticSkill"));

        return new ParsedTeam(ReadInt(node, idName), data);
    }

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = ReadText(parent, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double ReadDouble(XElement parent, string name, double fallback = 0)
    {
        var text = ReadText(parent, name);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static DateTime ReadDate(XElement parent, string name)
    {
        var text = ReadText(parent, name);
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value))
            return value;
        return DateTime.MinValue;
    }

    private static string? ReadText(XElement parent, string name)
    {
        return parent.Element(name)?.Value?.Trim();
    }

    private sealed record MatchTeam(int TeamId, string TeamName);

    private sealed record MatchSummary(
        int MatchId,
        DateTime MatchDate,
        string Status,
        int HomeTeamId,
        string HomeTeamName,
        int AwayTeamId,
        string AwayTeamName);

    private sealed record ParsedTeam(int TeamId, TeamData Data);

    private sealed record ParsedMatchDetails(
        int MatchId,
        DateTime MatchDate,
        ParsedTeam HomeTeam,
        ParsedTeam AwayTeam);
}
