using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M5XICandidatesRegression
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var normalized = root.GetProperty("normalized");
            var analysis = root.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var opponentRating = ReadRating(analysis.GetProperty("opponentRating"));
            var opponentName = GetString(analysis, "opponentName", "Opponent");
            var fixtureLineup = analysis.GetProperty("ownLineup");
            var teamName = GetString(fixtureLineup, "teamName", "Fixture");
            var opponentFormation = GetString(analysis, "opponentFormation", "");
            var opponent = new OpponentMatchProfile(opponentName, opponentFormation, opponentRating, new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(players, 0, teamName, opponent, RatingContext.Default, MatchQuestionnaire.Default);

            Console.WriteLine("=== C3 M5 XI CANDIDATES REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c3-m5");
            var legal = result.M4.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal).ToList();
            var eligibleIds = players.Where(x => x.InjuryLevel != 999 && x.Id > 0).Select(x => x.Id).ToHashSet();

            Check(result.M5.Count > 0, "M5 produced XI candidates");
            Check(legal.All(f => result.M5.Any(x => x.Formation == f)), "M5 covers every M4 legal formation");
            Check(result.M5.GroupBy(x => x.Formation).All(g => g.Count() <= 20), "M5 respects max 20 candidates per formation");
            Check(result.M5.All(x => x.Lineup.Slots.Count == 11), "every M5 XI has exactly 11 slots");
            Check(result.M5.All(x => x.Lineup.Slots.Select(s => s.PlayerId).Distinct().Count() == 11), "every M5 XI uses 11 unique players");
            Check(result.M5.All(x => x.Lineup.Slots.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count() == 11), "every M5 XI uses 11 unique slot codes");
            Check(result.M5.All(x => x.Lineup.Formation == x.Formation), "XI formation identity is preserved");
            Check(result.M5.All(x => x.Lineup.Slots.All(s => eligibleIds.Contains(s.PlayerId))), "M5 never assigns an ineligible player");
            Check(result.M5.All(x => double.IsFinite(x.SuitabilityScore) && x.SuitabilityScore > 0), "M5 suitability scores are finite and positive");
            Check(result.M5.All(x => double.IsFinite(x.StructuralScore) && x.StructuralScore > 0), "M5 structural scores are finite and positive");
            Check(result.M5.GroupBy(x => x.Formation).All(g => g.Select(Signature).Distinct(StringComparer.Ordinal).Count() == g.Count()), "M5 candidates are unique per formation");

            Console.WriteLine($"M4 legal formations={legal.Count} | M5 XI candidates={result.M5.Count}");
            foreach (var group in result.M5.GroupBy(x => x.Formation).OrderBy(x => x.Key, StringComparer.Ordinal)) Console.WriteLine($"  {group.Key}: {group.Count()} candidates");
            Console.WriteLine("PASS: C3 M5 XI candidate contract");
            Console.WriteLine("NEXT: C4 M6-A candidate evaluation");
            return 0;
        }
        catch (Exception ex) { return Fail("C3 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e) => new(GetDouble(e, "leftDefence"), GetDouble(e, "centralDefence"), GetDouble(e, "rightDefence"), GetDouble(e, "midfield"), GetDouble(e, "leftAttack"), GetDouble(e, "centralAttack"), GetDouble(e, "rightAttack"));
    private static string Signature(PositionAssignmentCandidate x) => string.Join(";", x.Lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).Select(s => $"{s.Code}:{s.PlayerId}"));
    private static string GetString(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
