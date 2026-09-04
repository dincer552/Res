using System.Text.Json;
using System.Xml.Linq;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// Production-data acceptance gate for a historical CHPP corpus.
/// Collection is observation-only: no production coefficient is changed until
/// the corpus contains enough finished matches and detailed match observations.
/// </summary>
public static class HistoricalMultiMatchProductionAcceptance
{
    public const int MinimumFinishedMatches = 250;
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

            if (schema?.StartsWith("hattrickai-v5-historical-production", StringComparison.OrdinalIgnoreCase) == true)
                return RunProductionCorpus(root);

            // Backward-compatible validation for the original single-reference export.
            if (schema is null || !schema.StartsWith("hattrickai-v5-offline-test", StringComparison.OrdinalIgnoreCase))
                return Fail("Unsupported historical export schema.");
            if (!root.TryGetProperty("rawChpp", out var rawChpp))
                return Fail("rawChpp section is missing.");
            var own = ParseMatches(GetXml(rawChpp, "ownMatches"));
            var opponent = ParseMatches(GetXml(rawChpp, "opponentMatches"));
            var ownFinished = own.Where(IsFinished).ToArray();
            var opponentFinished = opponent.Where(IsFinished).ToArray();
            ValidateUniqueIds(ownFinished, "own historical matches");
            ValidateUniqueIds(opponentFinished, "opponent historical matches");
            ValidateFinishedScores(ownFinished, "own historical matches");
            ValidateFinishedScores(opponentFinished, "opponent historical matches");
            var detailed = CountDetailedRecords(rawChpp);
            var total = ownFinished.Length + opponentFinished.Length;
            Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: DATA_INCOMPLETE | legacyOwn={ownFinished.Length}; legacyOpponent={opponentFinished.Length}; totalFinished={total}; detailedMatchEngineRecords={detailed}");
            Console.WriteLine($"Production activation remains disabled. Required: >={MinimumFinishedMatches} finished matches and >={MinimumDetailedMatches} detailed CHPP match-engine records.");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int RunProductionCorpus(JsonElement root)
    {
        if (!root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Fail("Historical production corpus rows are missing.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var finished = 0;
        var detailed = 0;
        var invalid = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.TryGetProperty("matchId", out var idNode) ? idNode.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) { invalid++; continue; }
            if (row.TryGetProperty("error", out var errorNode) && !string.IsNullOrWhiteSpace(errorNode.ToString())) { invalid++; continue; }
            var homeGoals = GetNullableInt(row, "homeGoals");
            var awayGoals = GetNullableInt(row, "awayGoals");
            if (homeGoals is null || awayGoals is null || homeGoals < 0 || awayGoals < 0) { invalid++; continue; }
            finished++;
            if (HasDetailedObservation(row)) detailed++;
        }

        var archiveCount = GetInt(root, "archiveUniqueMatchCount");
        var ready = finished >= MinimumFinishedMatches && detailed >= MinimumDetailedMatches && invalid == 0;
        Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: {(ready ? "PASS" : "DATA_INCOMPLETE")} | archiveUnique={archiveCount}; finished={finished}; detailed={detailed}; invalid={invalid}");
        if (!ready)
            Console.WriteLine($"Production activation remains disabled. Required: >={MinimumFinishedMatches} valid finished matches and >={MinimumDetailedMatches} detailed observations with zero invalid rows.");
        else
            Console.WriteLine("Historical CHPP corpus is structurally accepted. Model calibration still requires metric-level evaluation before coefficients may change.");
        return 0;
    }

    private static bool HasDetailedObservation(JsonElement row)
        => HasNumber(row, "homeTactic") && HasNumber(row, "awayTactic")
        && HasNumber(row, "homeTacticSkill") && HasNumber(row, "awayTacticSkill")
        && HasNumber(row, "homeRatingMidfield") && HasNumber(row, "awayRatingMidfield")
        && HasNumber(row, "homeRatingLeftDef") && HasNumber(row, "awayRatingLeftDef")
        && HasNumber(row, "homeRatingMidDef") && HasNumber(row, "awayRatingMidDef")
        && HasNumber(row, "homeRatingRightDef") && HasNumber(row, "awayRatingRightDef")
        && HasNumber(row, "homeRatingLeftAtt") && HasNumber(row, "awayRatingLeftAtt")
        && HasNumber(row, "homeRatingMidAtt") && HasNumber(row, "awayRatingMidAtt")
        && HasNumber(row, "homeRatingRightAtt") && HasNumber(row, "awayRatingRightAtt")
        && HasNumber(row, "ownPossessionPercent")
        && HasNumber(row, "ownSectorChances") && HasNumber(row, "opponentSectorChances");

    private static bool HasNumber(JsonElement row, string name)
        => row.TryGetProperty(name, out var node) && node.ValueKind is JsonValueKind.Number;

    private static int GetInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var node) && node.TryGetInt32(out var value) ? value : 0;

    private static int? GetNullableInt(JsonElement row, string name)
        => row.TryGetProperty(name, out var node) && node.TryGetInt32(out var value) ? value : null;

    private static string GetXml(JsonElement rawChpp, string propertyName)
    {
        if (!rawChpp.TryGetProperty(propertyName, out var node))
            throw new InvalidOperationException($"Missing rawChpp.{propertyName}.");
        return node.GetString() ?? string.Empty;
    }

    private static MatchObservation[] ParseMatches(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) throw new InvalidOperationException("Historical match XML is empty.");
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Descendants("Match")
            .Select(m => new MatchObservation(GetString(m, "MatchID"), GetString(m, "Status"), GetNullableInt(m, "MatchType"), GetNullableInt(m, "HomeGoals"), GetNullableInt(m, "AwayGoals")))
            .Where(m => !string.IsNullOrWhiteSpace(m.MatchId)).ToArray();
    }

    private static int CountDetailedRecords(JsonElement rawChpp)
    {
        var count = 0;
        foreach (var property in rawChpp.EnumerateObject())
        {
            if (!property.Name.Contains("matchdetails", StringComparison.OrdinalIgnoreCase)) continue;
            var xml = property.Value.GetString();
            if (!string.IsNullOrWhiteSpace(xml) && HasDetailedMatchEngineInputs(xml)) count++;
        }
        return count;
    }

    private static bool HasDetailedMatchEngineInputs(string xml)
    {
        try
        {
            var match = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Descendants("Match").FirstOrDefault();
            var home = match?.Element("HomeTeam");
            var away = match?.Element("AwayTeam");
            return match is not null && home is not null && away is not null
                && match.Element("MatchID") is not null && match.Element("HomeGoals") is not null && match.Element("AwayGoals") is not null
                && home.Element("TacticSkill") is not null && home.Element("RatingMidfield") is not null
                && away.Element("TacticSkill") is not null && away.Element("RatingMidfield") is not null;
        }
        catch { return false; }
    }

    private static bool IsFinished(MatchObservation match) => string.Equals(match.Status, "FINISHED", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUniqueIds(IEnumerable<MatchObservation> matches, string label)
    {
        var duplicate = matches.GroupBy(m => m.MatchId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate match ID in {label}: {duplicate.Key}");
    }

    private static void ValidateFinishedScores(IEnumerable<MatchObservation> matches, string label)
    {
        foreach (var match in matches)
            if (match.HomeGoals is null || match.AwayGoals is null || match.HomeGoals < 0 || match.AwayGoals < 0)
                throw new InvalidOperationException($"Invalid final score in {label}: match {match.MatchId}");
    }

    private static string GetString(XElement parent, string name) => parent.Element(name)?.Value?.Trim() ?? string.Empty;
    private static int? GetNullableInt(XElement parent, string name) => int.TryParse(GetString(parent, name), out var value) ? value : null;
    private static int Fail(string message) { Console.WriteLine("HistoricalMultiMatchProductionAcceptance: FAIL | " + message); return 1; }
    private sealed record MatchObservation(string MatchId, string Status, int? MatchType, int? HomeGoals, int? AwayGoals);
}
