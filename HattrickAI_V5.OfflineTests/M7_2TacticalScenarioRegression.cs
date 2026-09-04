using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C6 acceptance: M7.2 exposed by the production pipeline must be the
/// AdvancedTacticalScenarioEngine result for the same selected XI/state as M7.
/// This verifies M7 -> M7.2 continuity without hard-coding fixture outputs.
/// </summary>
public static class M7_2TacticalScenarioRegression
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
            var opponentFormation = GetString(analysis, "opponentFormation", "");
            var fixtureLineup = analysis.GetProperty("ownLineup");
            var teamName = GetString(fixtureLineup, "teamName", "Fixture");
            var opponent = new OpponentMatchProfile(opponentName, opponentFormation, opponentRating, new OpponentThreatEngine().Analyze(opponentRating));
            var context = new MatchDataContext(players, 0, teamName, opponent, RatingContext.Default, MatchQuestionnaire.Default);

            Console.WriteLine("=== C6 M7.2 TACTICAL SCENARIO REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c6-m72");
            var m7 = result.M7;
            var m72 = result.M72;

            Check(m7 is not null, "production pipeline returned M7 result");
            Check(m72 is not null, "production pipeline returned M7.2 result");
            Check(m72.CandidateId == m7.State.CandidateId, "M7.2 CandidateId continues M7 CandidateId");
            Check(m72.CandidateId == m7.State.LineupId, "M7.2 CandidateId continues M7 LineupId");
            Check(m72.CandidateId == result.FinalPlan.Lineup.Slots.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.PlayerId).Select(s => $"{s.Code}:{s.PlayerId}:{(int)s.Order}").Aggregate((a, b) => a + ";" + b), "M7.2 candidate identity matches selected XI signature");

            var tactic = Map(context.RatingContext.Tactic);
            Check(m72.Tactic == tactic, "M7.2 tactic matches current MatchDataContext tactic");
            Check(double.IsFinite(m72.TacticalSkillAggregate), "M7.2 tactical skill aggregate is finite");
            Check(m72.TacticalSkillAggregate >= 0 && m72.TacticalSkillAggregate <= 10, "M7.2 tactical skill aggregate is bounded");
            Check(double.IsFinite(m72.Level.Value) && m72.Level.Value >= 0 && m72.Level.Value <= 10, "M7.2 tactical level is finite and bounded");

            var inputs = new[]
            {
                m72.Inputs.TotalPassing, m72.Inputs.TotalDefending, m72.Inputs.TotalPlaymaking,
                m72.Inputs.TotalScoring, m72.Inputs.TotalWinger, m72.Inputs.TotalStamina,
                m72.Inputs.TotalExperience
            };
            Check(inputs.All(double.IsFinite) && inputs.All(x => x >= 0), "M7.2 tactical input totals are finite and non-negative");
            Check(inputs.Any(x => x > 0), "M7.2 tactical inputs are non-zero");

            var expectedOpponentAverage = Average(opponentRating);
            Check(Equal(m72.OpponentAverageMainSkill, expectedOpponentAverage), "M7.2 opponent average main skill matches production input");

            var direct = new AdvancedTacticalScenarioEngine().CalculateLineup(
                result.FinalPlan.Lineup,
                players,
                m7.State,
                expectedOpponentAverage);

            Check(direct.CandidateId == m72.CandidateId, "direct M7.2 recalculation preserves candidate identity");
            Check(direct.Tactic == m72.Tactic, "M7.2 tactic matches direct production recalculation");
            Check(Equal(direct.TacticalSkillAggregate, m72.TacticalSkillAggregate), "M7.2 tactical skill matches direct production recalculation");
            Check(direct.Level.Name == m72.Level.Name && Equal(direct.Level.Value, m72.Level.Value), "M7.2 level matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalPassing, m72.Inputs.TotalPassing), "M7.2 passing input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalDefending, m72.Inputs.TotalDefending), "M7.2 defending input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalPlaymaking, m72.Inputs.TotalPlaymaking), "M7.2 playmaking input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalScoring, m72.Inputs.TotalScoring), "M7.2 scoring input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalWinger, m72.Inputs.TotalWinger), "M7.2 winger input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalStamina, m72.Inputs.TotalStamina), "M7.2 stamina input matches direct production recalculation");
            Check(Equal(direct.Inputs.TotalExperience, m72.Inputs.TotalExperience), "M7.2 experience input matches direct production recalculation");
            Check(direct.M8Context.CandidateId == m72.M8Context.CandidateId, "M7.2 -> M8 candidate continuity is preserved");
            Check(direct.M8Context.Tactic == m72.M8Context.Tactic, "M7.2 -> M8 tactic continuity is preserved");
            Check(direct.M8Context.Level.Name == m72.M8Context.Level.Name && Equal(direct.M8Context.Level.Value, m72.M8Context.Level.Value), "M7.2 -> M8 tactical level continuity is preserved");

            Console.WriteLine($"M7.2 candidate={m72.CandidateId} | tactic={m72.Tactic} | skill={m72.TacticalSkillAggregate:0.###} | level={m72.Level.Name} {m72.Level.Value:0.###}");
            Console.WriteLine("PASS: C6 M7.2 tactical scenario continuity");
            Console.WriteLine("NEXT: C7 M8 chance model");
            return 0;
        }
        catch (Exception ex) { return Fail("C6 exception: " + ex.Message); }
    }

    private static AdvancedTactic Map(TeamTactic tactic) => tactic switch
    {
        TeamTactic.CounterAttack => AdvancedTactic.CounterAttack,
        TeamTactic.LongShots => AdvancedTactic.LongShots,
        TeamTactic.AttackMiddle => AdvancedTactic.AttackMiddle,
        TeamTactic.AttackWings => AdvancedTactic.AttackWings,
        TeamTactic.Creative => AdvancedTactic.Creative,
        TeamTactic.Pressing => AdvancedTactic.Pressing,
        _ => AdvancedTactic.Normal
    };

    private static Player ReadPlayer(JsonElement e) => new(
        e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player",
        e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(),
        e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(),
        e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(),
        GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));

    private static RegionalRatingSnapshot ReadRating(JsonElement e)
    {
        var ld = GetDouble(e, "leftDefence"); var cd = GetDouble(e, "centralDefence"); var rd = GetDouble(e, "rightDefence");
        var mid = GetDouble(e, "midfield"); var la = GetDouble(e, "leftAttack"); var ca = GetDouble(e, "centralAttack"); var ra = GetDouble(e, "rightAttack");
        return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra);
    }

    private static double Average(RegionalRatingSnapshot r) =>
        (r.LeftDefence + r.CentralDefence + r.RightDefence + r.Midfield + r.LeftAttack + r.CentralAttack + r.RightAttack) / 7.0;

    private static string GetString(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-9;
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
