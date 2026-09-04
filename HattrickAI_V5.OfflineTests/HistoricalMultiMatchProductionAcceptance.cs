using System.Text.Json;
using System.Xml.Linq;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// Production-data acceptance gate for a multi-match historical CHPP corpus.
/// A corpus may only activate production calibration after enough finished
/// matches have complete match-engine observations; otherwise the gate reports
/// the data as validation-only and leaves the paper baseline untouched.
/// </summary>
public static class HistoricalMultiMatchProductionAcceptance
{
    public const int MinimumFinishedMatchesPerSide = 250;
    public const int MinimumDetailedMatches = 250;

    public static int Run(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: SKIP | fixture not found: {path}");
                return 0;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            var schema = root.TryGetProperty("schema", out var schemaNode) ? schemaNode.GetString() : null;
            var source = root.TryGetProperty("source", out var sourceNode) ? sourceNode.GetString() : null;
            if (!string.Equals(source, "CHPP", StringComparison.OrdinalIgnoreCase))
                return Fail("Historical export source is not CHPP.");
            if (schema is null || !schema.StartsWith("hattrickai-v5-offline-test", StringComparison.OrdinalIgnoreCase))
                return Fail("Unsupported historical export schema.");
            if (!root.TryGetProperty("rawChpp", out var rawChpp))
                return Fail("rawChpp section is missing.");

            var own = ParseMatches(GetXml(rawChpp, "ownMatches"), "ownMatches");
            var opponent = ParseMatches(GetXml(rawChpp, "opponentMatches"), "opponentMatches");
            var ownFinished = own.Where(IsFinished).ToArray();
            var opponentFinished = opponent.Where(IsFinished).ToArray();

            ValidateUniqueIds(ownFinished, "own historical matches");
            ValidateUniqueIds(opponentFinished, "opponent historical matches");
            ValidateFinishedScores(ownFinished, "own historical matches");
            ValidateFinishedScores(opponentFinished, "opponent historical matches");

            // The current offline export carries a single detailed reference match.
            // A production acceptance corpus requires one detailed record per
            // accepted match so ratings/tactic skill are observed without leakage.
            var detailedMatchCount = CountDetailedRecords(rawChpp);
            var totalFinished = ownFinished.Length + opponentFinished.Length;
            var ready = ownFinished.Length >= MinimumFinishedMatchesPerSide
                && opponentFinished.Length >= MinimumFinishedMatchesPerSide
                && detailedMatchCount >= MinimumDetailedMatches;

            Console.WriteLine(
                $"HistoricalMultiMatchProductionAcceptance: {(ready ? "PASS" : "DATA_INCOMPLETE")} | " +
                $"ownFinished={ownFinished.Length}; opponentFinished={opponentFinished.Length}; " +
                $"totalFinished={totalFinished}; detailedMatchEngineRecords={detailedMatchCount}");

            if (!ready)
            {
                Console.WriteLine(
                    "Production activation remains disabled. Required: " +
                    $">={MinimumFinishedMatchesPerSide} finished matches per side and " +
                    $">={MinimumDetailedMatches} detailed CHPP match-engine records.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static string GetXml(JsonElement rawChpp, string propertyName)
    {
        if (!rawChpp.TryGetProperty(propertyName, out var node))
            throw new InvalidOperationException($"Missing rawChpp.{propertyName}.");
        return node.GetString() ?? string.Empty;
    }

    private static MatchObservation[] ParseMatches(string xml, string label)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException($"Empty {label} XML.");

        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return document.Descendants("Match")
            .Select(match => new MatchObservation(
                GetString(match, "MatchID"),
                GetString(match, "Status"),
                GetNullableInt(match, "MatchType"),
                GetNullableInt(match, "HomeGoals"),
                GetNullableInt(match, "AwayGoals")))
            .Where(match => !string.IsNullOrWhiteSpace(match.MatchId))
            .ToArray();
    }

    private static int CountDetailedRecords(JsonElement rawChpp)
    {
        var count = 0;
        foreach (var property in rawChpp.EnumerateObject())
        {
            if (!property.Name.Contains("matchdetails", StringComparison.OrdinalIgnoreCase))
                continue;
            var xml = property.Value.GetString();
            if (!string.IsNullOrWhiteSpace(xml) && HasDetailedMatchEngineInputs(xml))
                count++;
        }
        return count;
    }

    private static bool IsFinished(MatchObservation match)
        => string.Equals(match.Status, "FINISHED", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUniqueIds(IEnumerable<MatchObservation> matches, string label)
    {
        var duplicates = matches
            .GroupBy(match => match.MatchId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException($"Duplicate match IDs in {label}: {string.Join(", ", duplicates)}");
    }

    private static void ValidateFinishedScores(IEnumerable<MatchObservation> matches, string label)
    {
        foreach (var match in matches)
        {
            if (match.HomeGoals is null || match.AwayGoals is null || match.HomeGoals < 0 || match.AwayGoals < 0)
                throw new InvalidOperationException($"Invalid final score in {label}: match {match.MatchId}");
        }
    }

    private static bool HasDetailedMatchEngineInputs(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var match = document.Descendants("Match").FirstOrDefault();
            if (match is null) return false;

            var home = match.Element("HomeTeam");
            var away = match.Element("AwayTeam");
            return home is not null && away is not null
                && match.Element("MatchID") is not null
                && match.Element("HomeGoals") is not null
                && match.Element("AwayGoals") is not null
                && home.Element("TacticSkill") is not null
                && home.Element("RatingMidfield") is not null
                && away.Element("TacticSkill") is not null
                && away.Element("RatingMidfield") is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetString(XElement parent, string name)
        => parent.Element(name)?.Value?.Trim() ?? string.Empty;

    private static int? GetNullableInt(XElement parent, string name)
        => int.TryParse(GetString(parent, name), out var value) ? value : null;

    private static int Fail(string message)
    {
        Console.WriteLine("HistoricalMultiMatchProductionAcceptance: FAIL | " + message);
        return 1;
    }

    private sealed record MatchObservation(string MatchId, string Status, int? MatchType, int? HomeGoals, int? AwayGoals);
}
