using System.Text.Json;

namespace HattrickAI.V5.Core;

public static class OfflineRegressionRunner
{
    public static async Task<int> RunAsync(string path)
    {
        if (!File.Exists(path)) return Fail($"offline JSON bulunamadı: {path}");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var normalized = root.GetProperty("normalized");
            var analysis = root.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var lineup = ReadLineup(analysis.GetProperty("ownLineup"));
            var opponent = ReadRating(analysis.GetProperty("opponentRating"));

            var failures = new List<string>();
            Check(lineup.Slots.Count == 11, "M7 XI 11 oyuncu", failures);
            Check(players.Count >= 11, "own player pool >= 11", failures);
            Check(IsFiniteRating(opponent), "opponent 7 rating finite", failures);

            var m7 = new RegionalRatingScenarioEngine();
            var m72 = new AdvancedTacticalScenarioEngine();
            var m8 = new M8ChanceModel();
            var state = new MatchState("offline-m7", analysis.GetProperty("ownFormation").GetString() ?? "3-5-2", "offline-xi", "offline", MatchLocation.Away, TeamAttitude.Normal, TeamTactic.Normal, 4.5);
            var m7Result = m7.CalculateLineup(lineup, players, state);
            Check(IsFiniteRating(m7Result.Rating), "M7 rating finite", failures);
            Check(Math.Abs(RegionalRatingScenarioEngine.TeamSpiritMultiplier(4.5) - 1.0) < 0.01, "M7.1 composed spirit baseline", failures);

            foreach (var tactic in Enum.GetValues<TeamTactic>())
            {
                var tacticState = state with { CandidateId = "offline-" + tactic, TeamTactic = tactic };
                var t = m72.CalculateLineup(lineup, players, tacticState, OpponentAverage(opponent));
                var handoff = AdvancedTacticalScenarioEngine.BuildM8Input(m7.CalculateLineup(lineup, players, tacticState), t);
                var chance = m8.Calculate(handoff, opponent);
                Check(t.CandidateId == handoff.CandidateId, $"M7.2→M8 candidate {tactic}", failures);
                Check(t.Level.Value is >= 0 and <= 10, $"M7.2 level bounds {tactic}", failures);
                Check(Math.Abs(t.ChanceDistribution.LeftShare + t.ChanceDistribution.CentreShare + t.ChanceDistribution.RightShare + t.ChanceDistribution.SetPieceShare - 1.0) < 1e-9, $"M7.2 distribution sum {tactic}", failures);
                Check(chance.MidfieldShare is >= 0 and <= 1 && chance.StructuralChanceIndex is >= 0 and <= 1, $"M8 bounds {tactic}", failures);
            }

            if (failures.Count > 0) { foreach (var f in failures) Console.WriteLine("FAIL: " + f); return 1; }
            Console.WriteLine("PASS: M7 → M7.1 → M7.2 → M8 offline regression");
            Console.WriteLine($"XI: {lineup.Formation} | Opponent: {analysis.GetProperty("opponentName").GetString()}");
            Console.WriteLine($"M7 midfield: {m7Result.Rating.Midfield:0.###} | Opponent midfield: {opponent.Midfield:0.###}");
            Console.WriteLine("Tactics tested: Normal, CounterAttack, LongShots, AttackMiddle, AttackWings, Creative");
            return 0;
        }
        catch (Exception ex) { return Fail("offline regression exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), e.TryGetProperty("loyalty", out var l) ? l.GetInt32() : 0);
    private static Lineup ReadLineup(JsonElement e)
    {
        var slots = e.GetProperty("slots").EnumerateArray().Select(s => new Slot(s.GetProperty("code").GetString() ?? "", s.GetProperty("label").GetString() ?? "", s.GetProperty("description").GetString() ?? "", s.TryGetProperty("playerName", out var n) ? n.GetString() : null, s.GetProperty("playerId").GetInt32(), s.GetProperty("rating").GetDouble(), s.GetProperty("x").GetDouble(), s.GetProperty("y").GetDouble())).ToList();
        return new Lineup(e.GetProperty("teamName").GetString() ?? "", e.GetProperty("formation").GetString() ?? "", slots);
    }
    private static RegionalRatingSnapshot ReadRating(JsonElement e) => new(e.GetProperty("rawLeftDefence").GetDouble(), e.GetProperty("rawCentralDefence").GetDouble(), e.GetProperty("rawRightDefence").GetDouble(), e.GetProperty("rawMidfield").GetDouble(), e.GetProperty("rawLeftAttack").GetDouble(), e.GetProperty("rawCentralAttack").GetDouble(), e.GetProperty("rawRightAttack").GetDouble(), e.GetProperty("leftDefence").GetDouble(), e.GetProperty("centralDefence").GetDouble(), e.GetProperty("rightDefence").GetDouble(), e.GetProperty("midfield").GetDouble(), e.GetProperty("leftAttack").GetDouble(), e.GetProperty("centralAttack").GetDouble(), e.GetProperty("rightAttack").GetDouble());
    private static double OpponentAverage(RegionalRatingSnapshot r) => (r.LeftDefence + r.CentralDefence + r.RightDefence + r.Midfield + r.LeftAttack + r.CentralAttack + r.RightAttack) / 7.0;
    private static bool IsFiniteRating(RegionalRatingSnapshot r) => new[]{r.LeftDefence,r.CentralDefence,r.RightDefence,r.Midfield,r.LeftAttack,r.CentralAttack,r.RightAttack}.All(double.IsFinite);
    private static void Check(bool ok,string name,List<string> failures){if(!ok)failures.Add(name);}
    private static int Fail(string message){Console.WriteLine("FAIL: "+message);return 1;}
}
