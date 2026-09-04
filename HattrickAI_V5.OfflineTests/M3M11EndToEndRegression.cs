using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C1 acceptance: current M3 input/output continuity into the live pipeline.
/// The test must use the production PlayerAnalysisEngine and MotorPipelineService;
/// fixture-only JSON shape checks are not sufficient evidence of M3 behavior.
/// </summary>
public static class M3M11EndToEndRegression
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

            Console.WriteLine("=== C1 M3 INPUT/OUTPUT CONTINUITY REGRESSION ===");
            Check(players.Count >= 11, "M3 input player pool >= 11");
            Check(players.Select(x => x.Id).Distinct().Count() == players.Count, "M3 input player IDs unique");

            // Independent production M3 checkpoint.
            var expectedM3 = new PlayerAnalysisEngine().Analyze(players);
            Check(expectedM3.Players.Count == players.Count, "M3 emits exactly one profile per input player");

            foreach (var input in players)
            {
                var profile = expectedM3.Players.Single(x => x.PlayerId == input.Id);
                var eligible = input.Id > 0 && input.InjuryLevel != 999;
                Check(profile.PlayerName == input.Name, $"M3 preserves player name for {input.Id}");
                Check(profile.IsEligible == eligible, $"M3 eligibility mismatch for {input.Id}");
                Check(profile.InjuryLevel == input.InjuryLevel, $"M3 preserves injury level for {input.Id}");
                Check(profile.Specialty == input.Specialty, $"M3 preserves specialty for {input.Id}");
                Check(profile.Positions.Count == (eligible ? 14 : 0), $"M3 position universe mismatch for {input.Id}");
                if (eligible)
                {
                    Check(profile.PrimaryPosition == profile.Positions[0].PositionCode, $"M3 primary position continuity for {input.Id}");
                    Check(profile.SecondaryPosition == profile.Positions[1].PositionCode, $"M3 secondary position continuity for {input.Id}");
                    Check(profile.Positions.All(x => double.IsFinite(x.Score)), $"M3 position scores are finite for {input.Id}");
                }
                else
                {
                    Check(profile.PrimaryPosition is null && profile.SecondaryPosition is null, $"M3 clears positions for ineligible player {input.Id}");
                }
            }

            // Continuity checkpoint: the same production M3 output must be accepted by
            // the current M4/M5+ pipeline without a fixture-specific adapter.
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c1-m3");
            Check(result.M3.Players.Count == expectedM3.Players.Count, "live pipeline preserves M3 profile count");
            Check(result.M3.Players.Select(x => x.PlayerId).OrderBy(x => x).SequenceEqual(expectedM3.Players.Select(x => x.PlayerId).OrderBy(x => x)), "live pipeline preserves M3 player identity");
            foreach (var expected in expectedM3.Players)
            {
                var actual = result.M3.Players.Single(x => x.PlayerId == expected.PlayerId);
                Check(actual.PlayerName == expected.PlayerName, $"live M3 preserves name for {expected.PlayerId}");
                Check(actual.IsEligible == expected.IsEligible, $"live M3 preserves eligibility for {expected.PlayerId}");
                Check(actual.InjuryLevel == expected.InjuryLevel, $"live M3 preserves injury level for {expected.PlayerId}");
                Check(actual.Specialty == expected.Specialty, $"live M3 preserves specialty for {expected.PlayerId}");
                Check(actual.Positions.Count == expected.Positions.Count, $"live M3 preserves position count for {expected.PlayerId}");
                for (var i = 0; i < expected.Positions.Count; i++)
                {
                    Check(actual.Positions[i].PositionCode == expected.Positions[i].PositionCode, $"live M3 position code changed for {expected.PlayerId}");
                    Check(Math.Abs(actual.Positions[i].Score - expected.Positions[i].Score) < 1e-12, $"live M3 position score changed for {expected.PlayerId}");
                }
            }

            Check(result.M4.Candidates.Count > 0, "M4 accepts current M3 output");
            Check(result.M5.Count > 0, "M5 accepts the M3→M4 output chain");

            Console.WriteLine($"Input players={players.Count} | M3 profiles={result.M3.Players.Count} | M4 legal={result.M4.Candidates.Count} | M5 XI={result.M5.Count}");
            Console.WriteLine("PASS: C1 M3 input/output continuity");
            Console.WriteLine("NEXT: C2 M4 legal formations + authoritative registry");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("C1 exception: " + ex.Message);
        }
    }

    private static Player ReadPlayer(JsonElement e)
        => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));

    private static RegionalRatingSnapshot ReadRating(JsonElement e)
    {
        var ld = GetDouble(e, "leftDefence");
        var cd = GetDouble(e, "centralDefence");
        var rd = GetDouble(e, "rightDefence");
        var mid = GetDouble(e, "midfield");
        var la = GetDouble(e, "leftAttack");
        var ca = GetDouble(e, "centralAttack");
        var ra = GetDouble(e, "rightAttack");
        return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra);
    }

    private static string GetString(JsonElement e, string name, string fallback)
        => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static int GetInt(JsonElement e, string name, int fallback)
        => e.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;

    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
