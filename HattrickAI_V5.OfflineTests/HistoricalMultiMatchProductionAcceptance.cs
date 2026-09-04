using System.Text.Json;
using System.Xml.Linq;

namespace HattrickAI.V5.OfflineTests;

/// <summary>Acceptance gate for user-exported historical CHPP corpora.</summary>
public static class HistoricalMultiMatchProductionAcceptance
{
    public const int MinimumFinishedMatches = 60;
    public const int MinimumDetailedMatches = 60;

    public static int Run(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: SKIP | fixture not found: {path}"); return 0; }
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var schema = root.TryGetProperty("schema", out var sn) ? sn.GetString() : null;

            if (schema?.StartsWith("hattrickai-v5-m8-phase-d-calibration", StringComparison.OrdinalIgnoreCase) == true)
                return RunPhaseDCalibrationCorpus(root);

            var source = root.TryGetProperty("source", out var src) ? src.GetString() : null;
            if (!string.Equals(source, "CHPP", StringComparison.OrdinalIgnoreCase)) return Fail("Historical export source is not CHPP.");
            if (schema?.StartsWith("hattrickai-v5-historical-production", StringComparison.OrdinalIgnoreCase) == true) return RunProductionCorpus(root);
            if (schema is null || !schema.StartsWith("hattrickai-v5-offline-test", StringComparison.OrdinalIgnoreCase)) return Fail("Unsupported historical export schema.");
            if (!root.TryGetProperty("rawChpp", out var raw)) return Fail("rawChpp section is missing.");
            var own = ParseMatches(GetXml(raw,"ownMatches")); var opp = ParseMatches(GetXml(raw,"opponentMatches"));
            var of = own.Where(IsFinished).ToArray(); var pf = opp.Where(IsFinished).ToArray();
            ValidateUniqueIds(of,"own historical matches"); ValidateUniqueIds(pf,"opponent historical matches"); ValidateFinishedScores(of,"own historical matches"); ValidateFinishedScores(pf,"opponent historical matches");
            Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: DATA_INCOMPLETE | legacyOwn={of.Length}; legacyOpponent={pf.Length}; totalFinished={of.Length+pf.Length}; detailedMatchEngineRecords={CountDetailedRecords(raw)}");
            return 0;
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static int RunPhaseDCalibrationCorpus(JsonElement root)
    {
        var samples = root.TryGetProperty("samples", out var s) && s.ValueKind == JsonValueKind.Array ? s.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        var sourceRows = root.TryGetProperty("sourceRows", out var r) && r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        var sampleCount = GetInt(root,"sampleCount");
        var detailsFetched = GetNestedInt(root,"sourceSummary","detailsFetched");
        var failedDetails = GetNestedInt(root,"sourceSummary","failedDetails");
        var chanceSamples = GetNestedInt(root,"sourceSummary","chanceSamples");
        var archiveUnique = GetNestedInt(root,"sourceSummary","archiveUniqueMatchCount");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var invalidSamples = 0;
        foreach (var sample in samples)
        {
            var id = sample.TryGetProperty("matchId", out var idNode) ? idNode.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id) || GetNullableInt(sample,"ownGoals") is null || GetNullableInt(sample,"opponentGoals") is null) { invalidSamples++; continue; }
            if (!HasNumber(sample,"midfieldShare") || !HasNumber(sample,"observedTotalRegularChances") || !HasNumber(sample,"observedOwnRegularChances") || !HasNumber(sample,"observedOpponentRegularChances") || !HasNumber(sample,"ownTactic") || !HasNumber(sample,"opponentTactic") || !HasNumber(sample,"ownTacticSkill") || !HasNumber(sample,"opponentTacticSkill")) invalidSamples++;
        }

        var invalidSourceRows = 0;
        foreach (var row in sourceRows)
        {
            var id = row.TryGetProperty("matchId", out var idNode) ? idNode.ToString() : string.Empty;
            var valid = !string.IsNullOrWhiteSpace(id)
                && HasNumber(row,"ownPossessionPercent")
                && HasChanceObject(row,"ownSectorChances")
                && HasChanceObject(row,"opponentSectorChances")
                && HasNumber(row,"homeTactic") && HasNumber(row,"awayTactic")
                && HasNumber(row,"homeTacticSkill") && HasNumber(row,"awayTacticSkill")
                && HasNumber(row,"homeRatingMidfield") && HasNumber(row,"awayRatingMidfield")
                && HasNumber(row,"homeRatingLeftDef") && HasNumber(row,"homeRatingMidDef") && HasNumber(row,"homeRatingRightDef")
                && HasNumber(row,"awayRatingLeftDef") && HasNumber(row,"awayRatingMidDef") && HasNumber(row,"awayRatingRightDef")
                && HasNumber(row,"homeRatingLeftAtt") && HasNumber(row,"homeRatingMidAtt") && HasNumber(row,"homeRatingRightAtt")
                && HasNumber(row,"awayRatingLeftAtt") && HasNumber(row,"awayRatingMidAtt") && HasNumber(row,"awayRatingRightAtt")
                && GetNullableInt(row,"homeGoals") is not null && GetNullableInt(row,"awayGoals") is not null;
            if (!valid) invalidSourceRows++;
        }

        var ready = sampleCount >= MinimumFinishedMatches
            && samples.Length >= MinimumFinishedMatches
            && sourceRows.Length >= MinimumDetailedMatches
            && detailsFetched >= MinimumDetailedMatches
            && chanceSamples >= MinimumDetailedMatches
            && failedDetails == 0
            && archiveUnique >= MinimumFinishedMatches
            && invalidSamples == 0
            && invalidSourceRows == 0;

        var setPieceObserved = samples.Count(x => x.TryGetProperty("observedOwnSetPieceChances", out var n) && n.ValueKind != JsonValueKind.Null);
        Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: {(ready ? "PASS" : "DATA_INCOMPLETE")} | phaseD samples={samples.Length}; sourceRows={sourceRows.Length}; detailsFetched={detailsFetched}; failedDetails={failedDetails}; chanceSamples={chanceSamples}; archiveUnique={archiveUnique}; invalidSamples={invalidSamples}; invalidSourceRows={invalidSourceRows}; setPieceSampleFieldPresent={setPieceObserved}");
        if (setPieceObserved == 0) Console.WriteLine("Phase D note: observedOwnSetPieceChances is null in all sample rows; raw sourceRows still expose home/away special-event chances.");
        if (!ready) Console.WriteLine($"60-match acceptance requires >={MinimumFinishedMatches} samples, >={MinimumDetailedMatches} detailed source rows, zero failed details and zero invalid rows.");
        else Console.WriteLine("60-match CHPP-derived calibration corpus structurally accepted. Coefficients remain unchanged.");
        return 0;
    }

    private static int RunProductionCorpus(JsonElement root)
    {
        if (!root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array) return Fail("Historical production corpus rows are missing.");
        var seen = new HashSet<string>(StringComparer.Ordinal); var finished = 0; var detailed = 0; var invalid = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.TryGetProperty("matchId", out var idNode) ? idNode.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) { invalid++; continue; }
            if (row.TryGetProperty("error", out var err) && !string.IsNullOrWhiteSpace(err.ToString())) { invalid++; continue; }
            var hg = GetNullableInt(row,"homeGoals"); var ag = GetNullableInt(row,"awayGoals");
            if (hg is null || ag is null || hg < 0 || ag < 0) { invalid++; continue; }
            finished++; if (HasDetailedObservation(row)) detailed++;
        }
        var archiveCount = GetInt(root,"archiveUniqueMatchCount");
        var ready = finished >= MinimumFinishedMatches && detailed >= MinimumDetailedMatches && invalid == 0;
        Console.WriteLine($"HistoricalMultiMatchProductionAcceptance: {(ready ? "PASS" : "DATA_INCOMPLETE")} | archiveUnique={archiveCount}; finished={finished}; detailed={detailed}; invalid={invalid}");
        if (!ready) Console.WriteLine($"Production activation remains disabled. Required: >={MinimumFinishedMatches} valid finished matches and >={MinimumDetailedMatches} detailed observations with zero invalid rows.");
        else Console.WriteLine("Historical CHPP corpus structurally accepted. Metric-level model evaluation is still required before changing coefficients.");
        return 0;
    }

    private static bool HasDetailedObservation(JsonElement row)
        => HasNumber(row,"ownTactic") && HasNumber(row,"opponentTactic") && HasNumber(row,"ownTacticSkill") && HasNumber(row,"opponentTacticSkill")
        && HasNumber(row,"ownRatingMidfield") && HasNumber(row,"opponentRatingMidfield")
        && HasNumber(row,"ownRatingLeftDef") && HasNumber(row,"opponentRatingLeftDef")
        && HasNumber(row,"ownRatingMidDef") && HasNumber(row,"opponentRatingMidDef")
        && HasNumber(row,"ownRatingRightDef") && HasNumber(row,"opponentRatingRightDef")
        && HasNumber(row,"ownRatingLeftAtt") && HasNumber(row,"opponentRatingLeftAtt")
        && HasNumber(row,"ownRatingMidAtt") && HasNumber(row,"opponentRatingMidAtt")
        && HasNumber(row,"ownRatingRightAtt") && HasNumber(row,"opponentRatingRightAtt")
        && HasNumber(row,"ownPossessionPercent") && HasChanceObject(row,"ownSectorChances") && HasChanceObject(row,"opponentSectorChances");

    private static bool HasNumber(JsonElement row,string name) => row.TryGetProperty(name,out var n) && n.ValueKind == JsonValueKind.Number;
    private static bool HasChanceObject(JsonElement row,string name) => row.TryGetProperty(name,out var n) && n.ValueKind == JsonValueKind.Object && HasNumber(n,"left") && HasNumber(n,"center") && HasNumber(n,"right") && HasNumber(n,"specialEvents") && HasNumber(n,"other");
    private static int GetInt(JsonElement root,string name) => root.TryGetProperty(name,out var n) && n.TryGetInt32(out var v) ? v : 0;
    private static int GetNestedInt(JsonElement root,string parent,string name) => root.TryGetProperty(parent,out var p) && p.TryGetProperty(name,out var n) && n.TryGetInt32(out var v) ? v : 0;
    private static int? GetNullableInt(JsonElement row,string name) => row.TryGetProperty(name,out var n) && n.TryGetInt32(out var v) ? v : null;
    private static string GetXml(JsonElement raw,string name) => raw.TryGetProperty(name,out var n) ? n.GetString() ?? string.Empty : throw new InvalidOperationException($"Missing rawChpp.{name}.");
    private static MatchObservation[] ParseMatches(string xml) => XDocument.Parse(xml).Descendants("Match").Select(m => new MatchObservation(GetString(m,"MatchID"),GetString(m,"Status"),GetNullableInt(m,"HomeGoals"),GetNullableInt(m,"AwayGoals"))).Where(x=>x.MatchId.Length>0).ToArray();
    private static int CountDetailedRecords(JsonElement raw) => raw.EnumerateObject().Count(p=>p.Name.Contains("matchdetails",StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Value.GetString()) && HasDetailedMatchEngineInputs(p.Value.GetString()!));
    private static bool HasDetailedMatchEngineInputs(string xml){try{var m=XDocument.Parse(xml).Descendants("Match").FirstOrDefault();var h=m?.Element("HomeTeam");var a=m?.Element("AwayTeam");return m is not null&&h is not null&&a is not null&&m.Element("MatchID") is not null&&h.Element("TacticSkill") is not null&&a.Element("TacticSkill") is not null&&h.Element("RatingMidfield") is not null&&a.Element("RatingMidfield") is not null;}catch{return false;}}
    private static bool IsFinished(MatchObservation m)=>string.Equals(m.Status,"FINISHED",StringComparison.OrdinalIgnoreCase);
    private static void ValidateUniqueIds(IEnumerable<MatchObservation> ms,string label){var d=ms.GroupBy(x=>x.MatchId,StringComparer.Ordinal).FirstOrDefault(g=>g.Count()>1);if(d is not null)throw new InvalidOperationException($"Duplicate match ID in {label}: {d.Key}");}
    private static void ValidateFinishedScores(IEnumerable<MatchObservation> ms,string label){foreach(var m in ms)if(m.HomeGoals is null||m.AwayGoals is null||m.HomeGoals<0||m.AwayGoals<0)throw new InvalidOperationException($"Invalid final score in {label}: match {m.MatchId}");}
    private static string GetString(XElement e,string n)=>e.Element(n)?.Value?.Trim()??string.Empty;
    private static int? GetNullableInt(XElement e,string n)=>int.TryParse(GetString(e,n),out var v)?v:null;
    private static int Fail(string m){Console.WriteLine("HistoricalMultiMatchProductionAcceptance: FAIL | "+m);return 1;}
    private sealed record MatchObservation(string MatchId,string Status,int? HomeGoals,int? AwayGoals);
}
