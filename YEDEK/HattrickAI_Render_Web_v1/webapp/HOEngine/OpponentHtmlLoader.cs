using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace HattrickAI.HOEngine;

public class OpponentHtmlLoader
{
    public OpponentMatchData Load(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new InvalidDataException("Match report is empty.");

        string source = WebUtility.HtmlDecode(html);

        Match reportMatch = Regex.Match(
            source,
            @"<match-report\s+[^>]*match-id\s*=\s*[""'](?<id>[^""']+)[""']" +
            @"[^>]*home-team\s*=\s*[""'](?<home>[^""']+)[""']" +
            @"[^>]*away-team\s*=\s*[""'](?<away>[^""']+)[""']",
            RegexOptions.IgnoreCase);

        if (!reportMatch.Success)
        {
            throw new InvalidDataException(
                "Clean match report format was not found.");
        }

        MatchCollection ratingMatches = Regex.Matches(
            source,
            @"<rating\s+[^>]*home\s*=\s*[""'](?<home>[0-9.,]+)[""']" +
            @"[^>]*away\s*=\s*[""'](?<away>[0-9.,]+)[""']",
            RegexOptions.IgnoreCase);

        if (ratingMatches.Count != 7)
        {
            throw new InvalidDataException(
                "Seven sector ratings were not found.");
        }

        var homeValues = new double[7];
        var awayValues = new double[7];

        for (int i = 0; i < ratingMatches.Count; i++)
        {
            homeValues[i] = ParseRating(ratingMatches[i].Groups["home"].Value);
            awayValues[i] = ParseRating(ratingMatches[i].Groups["away"].Value);
        }

        var homeTeam = new TeamData(
            reportMatch.Groups["home"].Value.Trim(),
            CreateRatings(homeValues),
            0,
            0);

        var awayTeam = new TeamData(
            reportMatch.Groups["away"].Value.Trim(),
            CreateRatings(awayValues),
            0,
            0);

        return new OpponentMatchData(
            reportMatch.Groups["id"].Value.Trim(),
            homeTeam,
            awayTeam);
    }

    private static TeamRatings CreateRatings(double[] values)
    {
        return new TeamRatings(
            values[0],
            values[3],
            values[2],
            values[1],
            values[6],
            values[5],
            values[4]);
    }

    private static double ParseRating(string value)
    {
        return double.Parse(
            value.Replace(',', '.'),
            CultureInfo.InvariantCulture);
    }
}
