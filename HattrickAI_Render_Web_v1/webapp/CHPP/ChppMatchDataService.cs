using System.Globalization;
using System.Xml.Linq;
using HattrickAI.HOEngine;

namespace HattrickAI.CHPP;

public sealed record ChppFixture(
    int MatchId, DateTime MatchDate, int MatchType, string Status,
    int HomeTeamId, string HomeTeamName, int AwayTeamId, string AwayTeamName,
    int? HomeGoals, int? AwayGoals)
{
    public bool IsOwnHome(int ownTeamId) => HomeTeamId == ownTeamId;
    public int OpponentTeamId(int ownTeamId) => IsOwnHome(ownTeamId) ? AwayTeamId : HomeTeamId;
    public string OpponentName(int ownTeamId) => IsOwnHome(ownTeamId) ? AwayTeamName : HomeTeamName;
    public string VenueText(int ownTeamId) => IsOwnHome(ownTeamId) ? "Ev sahibi" : "Deplasman";
    public string ScoreText => HomeGoals.HasValue && AwayGoals.HasValue ? $"{HomeGoals}-{AwayGoals}" : "—";
    public bool IsStandardCup => MatchType == 3;
}

public sealed record ChppOpponentMatch(ChppFixture Fixture, TeamData OpponentTeam, TeamData OtherTeam);
public sealed record ChppSelectedMatch(ChppFixture Fixture, int OpponentTeamId, string OpponentTeamName, TeamData OwnTeamRatings, TeamData OpponentRatings, IReadOnlyList<ChppOpponentMatch> RecentMatches);

public sealed class ChppMatchDataService
{
    private readonly ChppOAuthClient _oauth;
    public ChppMatchDataService(ChppOAuthClient oauth) => _oauth = oauth;

    public async Task<IReadOnlyList<ChppFixture>> LoadUpcomingFixturesAsync(int ownTeamId, CancellationToken cancellationToken = default)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(_oauth, "matches",
            new Dictionary<string, string?> { ["version"] = "2.2", ["teamID"] = ownTeamId.ToString(CultureInfo.InvariantCulture) },
            $"upcoming fixtures ownTeamId={ownTeamId}", cancellationToken);
        return ParseMatches(xml)
            .Where(m => m.Status.Equals("UPCOMING", StringComparison.OrdinalIgnoreCase) || m.Status.Equals("ONGOING", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate >= DateTime.Now.AddMinutes(-10)).OrderBy(m => m.MatchDate).ToList();
    }

    public async Task<ChppFixture?> LoadLatestStandardCupFixtureAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var fixtures = await LoadRecentFixturesAsync(teamId, $"latest standard cup match teamId={teamId}", cancellationToken, 50);
        return fixtures.Where(x => x.IsStandardCup)
            .OrderByDescending(x => x.MatchDate)
            .FirstOrDefault();
    }

    public async Task<TeamData> LoadTeamDataFromHistoricalMatchAsync(ChppFixture fixture, int teamId, CancellationToken cancellationToken = default)
    {
        var parsed = await LoadMatchDetailsAsync(fixture,
            $"historical own matchDetails matchId={fixture.MatchId} teamId={teamId}", cancellationToken);
        return parsed.HomeTeam.TeamId == teamId ? parsed.HomeTeam.Data : parsed.AwayTeam.Data;
    }

    public async Task<ChppSelectedMatch> LoadSelectedMatchAsync(ChppFixture fixture, int ownTeamId, CancellationToken cancellationToken = default)
    {
        var opponentId = fixture.OpponentTeamId(ownTeamId);
        var opponentName = fixture.OpponentName(ownTeamId);
        if (opponentId <= 0) throw new InvalidDataException("Seçilen maçın rakip takım ID'si okunamadı.");

        var opponentRecent = await LoadRecentFixturesAsync(opponentId, $"opponent history teamId={opponentId} ({opponentName})", cancellationToken);
        var ownRecent = await LoadRecentFixturesAsync(ownTeamId, $"own history teamId={ownTeamId}", cancellationToken);
        if (opponentRecent.Count == 0) throw new InvalidDataException($"{opponentName} için tamamlanmış son maç bulunamadı.");
        if (ownRecent.Count == 0) throw new InvalidDataException("Kendi takımınız için tamamlanmış son maç bulunamadı; otomatik simülasyon ratingi üretilemedi.");

        var detailed = new List<ChppOpponentMatch>();
        foreach (var recent in opponentRecent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = await LoadMatchDetailsAsync(recent, $"opponent historical matchDetails matchId={recent.MatchId} opponentTeamId={opponentId} ({opponentName})", cancellationToken);
            var opponentTeam = parsed.HomeTeam.TeamId == opponentId ? parsed.HomeTeam.Data : parsed.AwayTeam.Data;
            var otherTeam = parsed.HomeTeam.TeamId == opponentId ? parsed.AwayTeam.Data : parsed.HomeTeam.Data;
            detailed.Add(new ChppOpponentMatch(recent, opponentTeam, otherTeam));
        }

        var ownDetailed = new List<TeamData>();
        foreach (var recent in ownRecent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = await LoadMatchDetailsAsync(recent, $"own historical matchDetails matchId={recent.MatchId} ownTeamId={ownTeamId}", cancellationToken);
            ownDetailed.Add(parsed.HomeTeam.TeamId == ownTeamId ? parsed.HomeTeam.Data : parsed.AwayTeam.Data);
        }

        return new ChppSelectedMatch(fixture, opponentId, opponentName,
            AverageTeamData("Kendi takımınız", ownDetailed),
            AverageTeamData(opponentName, detailed.Select(x => x.OpponentTeam)), detailed);
    }

    private async Task<List<ChppFixture>> LoadRecentFixturesAsync(int teamId, string context, CancellationToken cancellationToken, int take = 5)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(_oauth, "matches",
            new Dictionary<string, string?> { ["version"] = "2.2", ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture) },
            context, cancellationToken);
        return ParseMatches(xml).Where(m => m.Status.Equals("FINISHED", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.MatchDate < DateTime.Now.AddMinutes(5)).OrderByDescending(m => m.MatchDate).Take(Math.Max(1, take)).ToList();
    }

    private async Task<ParsedMatch> LoadMatchDetailsAsync(ChppFixture fixture, string context, CancellationToken cancellationToken)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(_oauth, "matchdetails",
            new Dictionary<string, string?> { ["version"] = "1.4", ["matchID"] = fixture.MatchId.ToString(CultureInfo.InvariantCulture) },
            context, cancellationToken);
        return ParseMatchDetails(xml, fixture);
    }

    private static IReadOnlyList<ChppFixture> ParseMatches(string xml)
    {
        var doc = XDocument.Parse(xml); var result = new List<ChppFixture>();
        foreach (var match in doc.Descendants("Match"))
        {
            var matchId = ReadInt(match, "MatchID"); var date = ReadDate(match, "MatchDate");
            var home = match.Element("HomeTeam"); var away = match.Element("AwayTeam");
            if (matchId <= 0 || date == DateTime.MinValue || home == null || away == null) continue;
            result.Add(new ChppFixture(matchId, date, ReadInt(match, "MatchType"), ReadText(match, "Status") ?? string.Empty,
                ReadInt(home, "HomeTeamID"), ReadText(home, "HomeTeamName") ?? "Ev sahibi",
                ReadInt(away, "AwayTeamID"), ReadText(away, "AwayTeamName") ?? "Deplasman",
                ReadNullableInt(home, "HomeGoals"), ReadNullableInt(away, "AwayGoals")));
        }
        return result;
    }

    private static ParsedMatch ParseMatchDetails(string xml, ChppFixture fallback)
    {
        var doc = XDocument.Parse(xml); var match = doc.Descendants("Match").FirstOrDefault() ?? throw new InvalidDataException("CHPP matchdetails XML içinde Match bulunamadı.");
        var home = match.Element("HomeTeam"); var away = match.Element("AwayTeam");
        if (home == null || away == null) throw new InvalidDataException("CHPP matchdetails XML takım bilgilerini içermiyor.");
        return new ParsedMatch(ReadInt(match, "MatchID", fallback.MatchId), ReadDate(match, "MatchDate", fallback.MatchDate),
            ParseTeam(home, "HomeTeamID", "HomeTeamName"), ParseTeam(away, "AwayTeamID", "AwayTeamName"));
    }

    private static ParsedTeam ParseTeam(XElement node, string idName, string nameName)
    {
        double Rating20(string elementName) => Math.Clamp(ReadDouble(node, elementName) / 4.0, 0.0, 20.0);
        var data = new TeamData(ReadText(node, nameName) ?? "Bilinmeyen Takım",
            new TeamRatings(Rating20("RatingMidfield"), Rating20("RatingLeftDef"), Rating20("RatingMidDef"), Rating20("RatingRightDef"),
                Rating20("RatingLeftAtt"), Rating20("RatingMidAtt"), Rating20("RatingRightAtt")),
            ReadInt(node, "TacticType"), ReadInt(node, "TacticSkill"));
        return new ParsedTeam(ReadInt(node, idName), data);
    }

    private static TeamData AverageTeamData(string name, IEnumerable<TeamData> teams)
    {
        var list = teams.ToList();
        if (list.Count == 0) throw new InvalidDataException("Maç geçmişlerinden rating üretilemedi.");
        double Avg(Func<TeamRatings, double> f) => list.Average(x => f(x.Ratings));
        var ratings = new TeamRatings(Avg(x => x.Midfield), Avg(x => x.LeftDefence), Avg(x => x.CentralDefence), Avg(x => x.RightDefence), Avg(x => x.LeftAttack), Avg(x => x.CentralAttack), Avg(x => x.RightAttack));
        return new TeamData(name, ratings, (int)Math.Round(list.Average(x => x.TacticType)), (int)Math.Round(list.Average(x => x.TacticLevel)));
    }

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    { var text = ReadText(parent, name); return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback; }
    private static int? ReadNullableInt(XElement parent, string name)
    { var text = ReadText(parent, name); return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null; }
    private static double ReadDouble(XElement parent, string name, double fallback = 0)
    { var text = ReadText(parent, name); return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback; }
    private static DateTime ReadDate(XElement parent, string name, DateTime fallback = default)
    { var text = ReadText(parent, name); return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value) ? value : fallback; }
    private static string? ReadText(XElement parent, string name) => parent.Element(name)?.Value?.Trim();

    private sealed record ParsedMatch(int MatchId, DateTime MatchDate, ParsedTeam HomeTeam, ParsedTeam AwayTeam);
    private sealed record ParsedTeam(int TeamId, TeamData Data);
}
