using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace HattrickAI.V5.Core;

public sealed class ReferenceMatchService
{
    private const int DefaultCalibrationLimit = 60;
    private const int MaxCalibrationLimit = 60;
    private const int ArchiveWindowDays = 45;

    private readonly ChppV5 _chpp;
    private readonly IHttpContextAccessor _http;

    public ReferenceMatchService(ChppV5 chpp, IHttpContextAccessor http)
    {
        _chpp = chpp;
        _http = http;
    }

    public async Task<object> GetAsync(CancellationToken ct)
    {
        if (string.Equals(_http.HttpContext?.Request.Query["calibration"], "1", StringComparison.Ordinal))
            return await CollectCalibration(ct);

        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string, string?> { ["version"] = "3.0" }, ct);
        var teamNode = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(teamNode, "TeamID");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım bilgisi alınamadı.");
        var ownLogoUrl = teamNode?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
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
        var logoCache = new Dictionary<int, string>();
        foreach (var match in upcomingLeague)
            if (!logoCache.ContainsKey(match.opponentTeamId))
                logoCache[match.opponentTeamId] = await ReadLogoUrl(match.opponentTeamId, ct);
        var upcomingWithLogos = upcomingLeague.Select(m => new
        {
            m.matchId, m.date, m.homeTeam, m.homeTeamId, m.awayTeam, m.awayTeamId,
            m.matchType, m.matchTypeName, m.isHome, m.opponentTeam, m.opponentTeamId,
            opponentLogoUrl = logoCache.GetValueOrDefault(m.opponentTeamId, string.Empty)
        }).ToList();
        var next = ownMatches.Where(m => m.Date > now && IsCompetitiveMatchType(m.MatchType)).OrderBy(m => m.Date).FirstOrDefault();
        if (next is null) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra yaklaşan resmi maç bulunamadı.");
        var opponentId = next.HomeTeamId == teamId ? next.AwayTeamId : next.HomeTeamId;
        var opponentMatches = await ReadMatches(opponentId, ct);
        var last = opponentMatches
            .Where(m => m.Date < now && m.HomeGoals.HasValue && m.AwayGoals.HasValue && IsCompetitiveMatchType(m.MatchType))
            .OrderByDescending(m => m.Date)
            .FirstOrDefault();
        if (last is null) throw new InvalidOperationException("Kupa ve hazırlık maçları atlandıktan sonra rakibin baz alınan resmi maçı bulunamadı.");
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

    private async Task<object> CollectCalibration(CancellationToken ct)
    {
        var teamXml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string, string?> { ["version"] = "3.0" }, ct);
        var team = XmlV5.Root(teamXml)?.Descendants("Team").FirstOrDefault();
        var teamId = XmlV5.Int(team, "TeamID");
        if (teamId <= 0) throw new InvalidOperationException("Kullanıcı takım ID'si alınamadı.");

        var requestedLimit = int.TryParse(_http.HttpContext?.Request.Query["limit"], out var n)
            ? n
            : DefaultCalibrationLimit;
        var limit = Math.Clamp(requestedLimit, 10, MaxCalibrationLimit);

        var endDate = DateTimeOffset.UtcNow.Date;
        var startDate = endDate.AddYears(-1);
        var archiveMatches = await ReadArchiveAcrossWindows(teamId, startDate, endDate, ct);
        var eligible = archiveMatches
            .Where(m => m.Date < DateTimeOffset.UtcNow
                        && m.HomeGoals.HasValue
                        && m.AwayGoals.HasValue
                        && IsCalibrationMatchType(m.MatchType))
            .OrderByDescending(m => m.Date)
            .Take(limit)
            .ToList();

        var rows = new List<object>(eligible.Count);
        var detailsFetched = 0;
        var chanceSamples = 0;
        var totalSector = 0;
        double possessionSum = 0;
        var failedDetails = 0;

        foreach (var match in eligible)
        {
            try
            {
                var xml = await _chpp.GetXmlAsync(
                    "matchdetails",
                    new Dictionary<string, string?>
                    {
                        ["version"] = "3.1",
                        ["matchID"] = match.MatchId.ToString(CultureInfo.InvariantCulture),
                        ["matchEvents"] = "true"
                    },
                    ct);

                var root = XmlV5.Root(xml);
                var node = root?.Descendants("Match").FirstOrDefault();
                if (node is null) continue;

                detailsFetched++;
                var home = node.Descendants("HomeTeam").FirstOrDefault();
                var away = node.Descendants("AwayTeam").FirstOrDefault();
                var hp = XmlV5.Int(node, "PossessionFirstHalfHome");
                var ap = XmlV5.Int(node, "PossessionFirstHalfAway");
                var hp2 = XmlV5.Int(node, "PossessionSecondHalfHome");
                var ap2 = XmlV5.Int(node, "PossessionSecondHalfAway");
                var ownHome = match.HomeTeamId == teamId;
                var ownPoss = ownHome ? (hp + hp2) / 2.0 : (ap + ap2) / 2.0;

                var homeLeft = XmlV5.Int(home, "NrOfChancesLeft");
                var homeCenter = XmlV5.Int(home, "NrOfChancesCenter");
                var homeRight = XmlV5.Int(home, "NrOfChancesRight");
                var awayLeft = XmlV5.Int(away, "NrOfChancesLeft");
                var awayCenter = XmlV5.Int(away, "NrOfChancesCenter");
                var awayRight = XmlV5.Int(away, "NrOfChancesRight");
                var homeSector = homeLeft + homeCenter + homeRight;
                var awaySector = awayLeft + awayCenter + awayRight;
                var ownSector = ownHome ? homeSector : awaySector;
                var opponentSector = ownHome ? awaySector : homeSector;
                totalSector += ownSector + opponentSector;
                possessionSum += ownPoss;
                chanceSamples++;

                rows.Add(new
                {
                    matchId = match.MatchId,
                    date = match.Date,
                    isHome = ownHome,
                    ownPossessionPercent = Math.Round(ownPoss, 2),
                    homePossessionFirstHalf = hp,
                    awayPossessionFirstHalf = ap,
                    homePossessionSecondHalf = hp2,
                    awayPossessionSecondHalf = ap2,
                    homeSectorChances = homeSector,
                    awaySectorChances = awaySector,
                    ownSectorChances = ownSector,
                    opponentSectorChances = opponentSector,
                    homeLeftChances = homeLeft,
                    homeCentreChances = homeCenter,
                    homeRightChances = homeRight,
                    awayLeftChances = awayLeft,
                    awayCentreChances = awayCenter,
                    awayRightChances = awayRight,
                    ownLeftChances = ownHome ? homeLeft : awayLeft,
                    ownCentreChances = ownHome ? homeCenter : awayCenter,
                    ownRightChances = ownHome ? homeRight : awayRight,
                    opponentLeftChances = ownHome ? awayLeft : homeLeft,
                    opponentCentreChances = ownHome ? awayCenter : homeCenter,
                    opponentRightChances = ownHome ? awayRight : homeRight,
                    homeOtherChances = XmlV5.Int(home, "NrOfChancesOther"),
                    awayOtherChances = XmlV5.Int(away, "NrOfChancesOther"),
                    homeSpecialEventChances = XmlV5.Int(home, "NrOfChancesSpecialEvents"),
                    awaySpecialEventChances = XmlV5.Int(away, "NrOfChancesSpecialEvents"),
                    homeGoals = match.HomeGoals,
                    awayGoals = match.AwayGoals,
                    homeTactic = XmlV5.Int(home, "TacticType"),
                    awayTactic = XmlV5.Int(away, "TacticType"),
                    homeTacticSkill = XmlV5.Int(home, "TacticSkill"),
                    awayTacticSkill = XmlV5.Int(away, "TacticSkill"),
                    homeRatingMidfield = XmlV5.Double(home, "RatingMidfield"),
                    awayRatingMidfield = XmlV5.Double(away, "RatingMidfield"),
                    homeRatingLeftDef = XmlV5.Double(home, "RatingLeftDef"),
                    homeRatingMidDef = XmlV5.Double(home, "RatingMidDef"),
                    homeRatingRightDef = XmlV5.Double(home, "RatingRightDef"),
                    homeRatingLeftAtt = XmlV5.Double(home, "RatingLeftAtt"),
                    homeRatingMidAtt = XmlV5.Double(home, "RatingMidAtt"),
                    homeRatingRightAtt = XmlV5.Double(home, "RatingRightAtt"),
                    awayRatingLeftDef = XmlV5.Double(away, "RatingLeftDef"),
                    awayRatingMidDef = XmlV5.Double(away, "RatingMidDef"),
                    awayRatingRightDef = XmlV5.Double(away, "RatingRightDef"),
                    awayRatingLeftAtt = XmlV5.Double(away, "RatingLeftAtt"),
                    awayRatingMidAtt = XmlV5.Double(away, "RatingMidAtt"),
                    awayRatingRightAtt = XmlV5.Double(away, "RatingRightAtt")
                });
            }
            catch (Exception ex)
            {
                failedDetails++;
                rows.Add(new { matchId = match.MatchId, date = match.Date, error = ex.Message });
            }
        }

        return new
        {
            ok = true,
            phase = "D",
            requestedLimit = limit,
            archiveWindowDays = ArchiveWindowDays,
            archiveWindowCount = CountArchiveWindows(startDate, endDate),
            archiveRawMatchCount = archiveMatches.Count,
            archiveUniqueMatchCount = archiveMatches.Select(x => x.MatchId).Distinct().Count(),
            sampleCount = eligible.Count,
            detailsFetched,
            failedDetails,
            chanceSamples,
            meanOwnPossessionPercent = chanceSamples == 0 ? 0 : Math.Round(possessionSum / chanceSamples, 2),
            totalObservedSectorChances = totalSector,
            notes = "Observation-only. 12-month archive is fetched in date-only windows. Sector chances are collected separately from Other/Special Event counts; production M8 coefficients are unchanged.",
            rows
        };
    }

    private async Task<List<ReferenceMatch>> ReadArchiveAcrossWindows(int teamId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
    {
        var all = new List<ReferenceMatch>();
        var cursorEnd = endDate;

        while (cursorEnd >= startDate)
        {
            var cursorStart = cursorEnd.AddDays(-(ArchiveWindowDays - 1));
            if (cursorStart < startDate) cursorStart = startDate;

            var window = await ReadArchiveWindow(teamId, cursorStart, cursorEnd, ct);
            all.AddRange(window);

            if (cursorStart <= startDate) break;
            cursorEnd = cursorStart.AddDays(-1);
        }

        return all
            .GroupBy(x => x.MatchId)
            .Select(g => g.OrderByDescending(x => x.Date).First())
            .OrderByDescending(x => x.Date)
            .ToList();
    }

    private async Task<List<ReferenceMatch>> ReadArchiveWindow(int teamId, DateTimeOffset first, DateTimeOffset last, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["version"] = "1.5",
            ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture),
            ["FirstMatchDate"] = first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["LastMatchDate"] = last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var xml = await _chpp.GetXmlAsync("matchesarchive", parameters, ct);
        var result = new List<ReferenceMatch>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m, "MatchID");
            var date = XmlV5.Date(m, "MatchDate");
            var homeId = XmlV5.Int(m, "HomeTeamID");
            var awayId = XmlV5.Int(m, "AwayTeamID");
            if (id <= 0 || date == default) continue;
            var type = XmlV5.Int(m, "MatchType");
            result.Add(new ReferenceMatch(
                id,
                date,
                XmlV5.Text(m, "HomeTeamName"),
                homeId,
                XmlV5.Text(m, "AwayTeamName"),
                awayId,
                NullableInt(m, "HomeGoals"),
                NullableInt(m, "AwayGoals"),
                type,
                MatchTypeName(type)));
        }
        return result;
    }

    private async Task<string> ReadLogoUrl(int teamId, CancellationToken ct)
    {
        if (teamId <= 0) return string.Empty;
        try
        {
            var xml = await _chpp.GetXmlAsync("teamdetails", new Dictionary<string, string?>
            {
                ["version"] = "3.0",
                ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture)
            }, ct);
            return XmlV5.Root(xml)?.Descendants("Team").FirstOrDefault()?.Descendants("LogoURL").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CountArchiveWindows(DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)Math.Ceiling((endDate.Date - startDate.Date).TotalDays / ArchiveWindowDays);

    private static bool IsCompetitiveMatchType(int type) => type is 1 or 2 or 7 or 10 or 11;
    private static bool IsCalibrationMatchType(int type) => type is 1 or 2 or 3 or 5 or 7;

    private async Task<List<ReferenceMatch>> ReadMatches(int teamId, CancellationToken ct)
    {
        var xml = await _chpp.GetXmlAsync("matches", new Dictionary<string, string?>
        {
            ["version"] = "1.3",
            ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var result = new List<ReferenceMatch>();
        foreach (var m in XmlV5.Root(xml)?.Descendants("Match") ?? Enumerable.Empty<XElement>())
        {
            var id = XmlV5.Int(m, "MatchID");
            var date = XmlV5.Date(m, "MatchDate");
            var homeId = XmlV5.Int(m, "HomeTeamID");
            var awayId = XmlV5.Int(m, "AwayTeamID");
            if (id <= 0 || date == default || (homeId != teamId && awayId != teamId)) continue;
            var type = XmlV5.Int(m, "MatchType");
            result.Add(new ReferenceMatch(
                id,
                date,
                XmlV5.Text(m, "HomeTeamName"),
                homeId,
                XmlV5.Text(m, "AwayTeamName"),
                awayId,
                NullableInt(m, "HomeGoals"),
                NullableInt(m, "AwayGoals"),
                type,
                MatchTypeName(type)));
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
        1 => "Lig",
        2 => "Play-off / Qualification",
        3 => "Kupa",
        4 => "Hazırlık",
        5 => "Hazırlık (kupa kuralları)",
        7 => "Hattrick Masters",
        8 => "Uluslararası hazırlık",
        9 => "Uluslararası hazırlık (kupa kuralları)",
        10 => "Milli takım resmi",
        11 => "Milli takım resmi (kupa kuralları)",
        12 => "Milli takım hazırlık",
        _ => $"Maç türü {type}"
    };

    private sealed record ReferenceMatch(
        int MatchId,
        DateTimeOffset Date,
        string HomeTeam,
        int HomeTeamId,
        string AwayTeam,
        int AwayTeamId,
        int? HomeGoals,
        int? AwayGoals,
        int MatchType,
        string MatchTypeName);
}
