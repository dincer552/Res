using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C5 acceptance: the M7 result exposed by the current production pipeline
/// must be a real RegionalRatingScenarioEngine output for the selected XI.
/// </summary>
public static class M7RegionalRatingRegression
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

            Console.WriteLine("=== C5 M7 REGIONAL RATING REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c5-m7");
            var m7 = result.M7;

            Check(m7 is not null, "production pipeline returned M7 result");
            Check(m7.Confidence == RatingConfidence.High, "M7 confidence is High");
            Check(!string.IsNullOrWhiteSpace(m7.State.CandidateId), "M7 candidate identity is populated");
            Check(!string.IsNullOrWhiteSpace(m7.State.FormationId), "M7 formation identity is populated");
            Check(!string.IsNullOrWhiteSpace(m7.State.LineupId), "M7 lineup identity is populated");
            Check(!string.IsNullOrWhiteSpace(m7.State.BehaviourSetId), "M7 behaviour-set identity is populated");
            Check(m7.State.FormationId == result.FinalPlan.Formation, "M7 selected formation matches FinalPlan");

            var rating = m7.Rating;
            var values = new[]
            {
                rating.LeftDefence, rating.CentralDefence, rating.RightDefence, rating.Midfield,
                rating.LeftAttack, rating.CentralAttack, rating.RightAttack,
                rating.RawLeftDefence, rating.RawCentralDefence, rating.RawRightDefence,
                rating.RawMidfield, rating.RawLeftAttack, rating.RawCentralAttack, rating.RawRightAttack
            };
            Check(values.All(double.IsFinite), "all M7 regional rating values are finite");
            Check(values.Any(x => x > 0), "M7 produces a non-zero regional rating");

            var direct = new RegionalRatingScenarioEngine().CalculateLineup(result.FinalPlan.Lineup, players, m7.State);
            Check(Equal(direct.Rating.LeftDefence, rating.LeftDefence), "M7 left defence matches direct production recalculation");
            Check(Equal(direct.Rating.CentralDefence, rating.CentralDefence), "M7 central defence matches direct production recalculation");
            Check(Equal(direct.Rating.RightDefence, rating.RightDefence), "M7 right defence matches direct production recalculation");
            Check(Equal(direct.Rating.Midfield, rating.Midfield), "M7 midfield matches direct production recalculation");
            Check(Equal(direct.Rating.LeftAttack, rating.LeftAttack), "M7 left attack matches direct production recalculation");
            Check(Equal(direct.Rating.CentralAttack, rating.CentralAttack), "M7 central attack matches direct production recalculation");
            Check(Equal(direct.Rating.RightAttack, rating.RightAttack), "M7 right attack matches direct production recalculation");
            Check(Equal(direct.Rating.RawMidfield, rating.RawMidfield), "M7 raw midfield matches direct production recalculation");

            Check(Equal(m7.Modifiers.TeamSpiritMultiplier, RegionalRatingScenarioEngine.TeamSpiritMultiplier(m7.State.TeamSpirit)), "M7 Team Spirit modifier matches production curve");
            var coach = RegionalRatingScenarioEngine.CoachStyleMultipliers(m7.State.CoachStyle);
            Check(Equal(m7.Modifiers.CoachAttackMultiplier, coach.AttackMultiplier), "M7 coach attack modifier matches production mapping");
            Check(Equal(m7.Modifiers.CoachDefenceMultiplier, coach.DefenceMultiplier), "M7 coach defence modifier matches production mapping");
            Check(m7.State.Confidence > 0 && double.IsFinite(m7.State.Confidence), "M7 state confidence is finite and positive");

            Console.WriteLine($"M7 formation={m7.State.FormationId} | confidence={m7.Confidence} | TS multiplier={m7.Modifiers.TeamSpiritMultiplier:0.###} | coach={m7.State.CoachStyle}");
            Console.WriteLine("PASS: C5 M7 regional rating continuity");
            Console.WriteLine("NEXT: C6 M7.2 tactical scenario");
            return 0;
        }
        catch (Exception ex) { return Fail("C5 exception: " + ex.Message); }
    }

    private static Player ReadPlayer(JsonElement e) => new(e.GetProperty("id").GetInt32(), e.GetProperty("name").GetString() ?? "Player", e.GetProperty("keeper").GetInt32(), e.GetProperty("defending").GetInt32(), e.GetProperty("playmaking").GetInt32(), e.GetProperty("passing").GetInt32(), e.GetProperty("winger").GetInt32(), e.GetProperty("scoring").GetInt32(), e.GetProperty("stamina").GetInt32(), e.GetProperty("form").GetInt32(), e.GetProperty("experience").GetInt32(), GetInt(e, "loyalty", 0), GetInt(e, "injuryLevel", -1));
    private static RegionalRatingSnapshot ReadRating(JsonElement e)
    {
        var ld = GetDouble(e, "leftDefence"); var cd = GetDouble(e, "centralDefence"); var rd = GetDouble(e, "rightDefence"); var mid = GetDouble(e, "midfield"); var la = GetDouble(e, "leftAttack"); var ca = GetDouble(e, "centralAttack"); var ra = GetDouble(e, "rightAttack");
        return new RegionalRatingSnapshot(ld, cd, rd, mid, la, ca, ra, ld, cd, rd, mid, la, ca, ra);
    }
    private static string GetString(JsonElement e, string name, string fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static int GetInt(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var value) ? value : fallback;
    private static double GetDouble(JsonElement e, string name) => e.GetProperty(name).GetDouble();
    private static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-9;
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
