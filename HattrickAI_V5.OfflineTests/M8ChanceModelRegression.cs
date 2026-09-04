using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C7 acceptance: M8 must consume the real M7 + M7.2 contract and reproduce
/// the current production chance-allocation result deterministically.
/// </summary>
public static class M8ChanceModelRegression
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

            Console.WriteLine("=== C7 M8 CHANCE MODEL REGRESSION ===");
            var result = await new MotorPipelineService().RunAsync(context, players, cancellationToken, "offline-c7-m8");
            Check(result.M7 is not null, "production pipeline returned M7");
            Check(result.M72 is not null, "production pipeline returned M7.2");
            Check(result.M8 is not null, "production pipeline returned M8");

            var m7 = result.M7!;
            var m72 = result.M72!;
            var m8 = result.M8!;
            var matchup = AdvancedTacticalScenarioEngine.BuildM8Input(m7, m72);
            Check(matchup.CandidateId == m8.CandidateId, "M7.2 → M8 CandidateId continuity");
            Check(matchup.FormationId == m7.State.FormationId, "M7 → M8 formation continuity");
            Check(matchup.LineupId == m7.State.LineupId, "M7 → M8 lineup continuity");
            Check(matchup.BehaviourSetId == m7.State.BehaviourSetId, "M7 → M8 behaviour-set continuity");
            Check(matchup.Tactic == m72.Tactic, "M7.2 tactic reaches M8 unchanged");
            Check(Equal(matchup.TacticalLevel.Value, m72.Level.Value), "M7.2 tactical level reaches M8 unchanged");
            Check(Equal(matchup.ChanceDistribution.LeftShare, m72.ChanceDistribution.LeftShare), "M7.2 left chance share reaches M8");
            Check(Equal(matchup.ChanceDistribution.CentreShare, m72.ChanceDistribution.CentreShare), "M7.2 centre chance share reaches M8");
            Check(Equal(matchup.ChanceDistribution.RightShare, m72.ChanceDistribution.RightShare), "M7.2 right chance share reaches M8");

            var direct = new M8ChanceModel().Calculate(matchup, opponentRating);
            Check(Equal(direct.MidfieldShare, m8.MidfieldShare), "M8 midfield share matches direct production calculation");
            Check(Equal(direct.LeftAttackVsRightDefence, m8.LeftAttackVsRightDefence), "M8 left attack matchup matches direct calculation");
            Check(Equal(direct.CentreAttackVsCentreDefence, m8.CentreAttackVsCentreDefence), "M8 centre attack matchup matches direct calculation");
            Check(Equal(direct.RightAttackVsLeftDefence, m8.RightAttackVsLeftDefence), "M8 right attack matchup matches direct calculation");
            Check(Equal(direct.StructuralChanceIndex, m8.StructuralChanceIndex), "M8 structural chance index matches direct calculation");
            Check(Equal(direct.OwnRegularChanceExpected, m8.OwnRegularChanceExpected), "M8 own regular chance volume matches direct calculation");
            Check(Equal(direct.OpponentRegularChanceExpected, m8.OpponentRegularChanceExpected), "M8 opponent regular chance volume matches direct calculation");
            Check(Equal(direct.TacticConversionRate, m8.TacticConversionRate), "M8 tactic conversion rate matches direct calculation");
            Check(Equal(direct.PressingSuppression, m8.PressingSuppression), "M8 pressing suppression matches direct calculation");
            Check(Equal(direct.CounterAttackChanceExpected, m8.CounterAttackChanceExpected), "M8 counter-attack expected chance matches direct calculation");
            Check(Equal(direct.LongShotChanceExpected, m8.LongShotChanceExpected), "M8 long-shot expected chance matches direct calculation");

            var values = new[]
            {
                m8.MidfieldShare, m8.LeftAttackVsRightDefence, m8.CentreAttackVsCentreDefence,
                m8.RightAttackVsLeftDefence, m8.LeftChanceShare, m8.CentreChanceShare,
                m8.RightChanceShare, m8.SetPieceChanceShare, m8.StructuralChanceIndex,
                m8.OwnRegularChanceExpected, m8.OpponentRegularChanceExpected,
                m8.TacticConversionRate, m8.LongShotConversionRate, m8.CounterAttackConversionRate,
                m8.PressingSuppression, m8.OwnRegularQuality, m8.OpponentRegularQuality
            };
            Check(values.All(double.IsFinite), "all M8 numeric outputs are finite");
            Check(m8.MidfieldShare >= 0 && m8.MidfieldShare <= 1, "M8 midfield share is bounded [0,1]");
            Check(m8.LeftChanceShare >= 0 && m8.LeftChanceShare <= 1, "M8 left chance share is bounded [0,1]");
            Check(m8.CentreChanceShare >= 0 && m8.CentreChanceShare <= 1, "M8 centre chance share is bounded [0,1]");
            Check(m8.RightChanceShare >= 0 && m8.RightChanceShare <= 1, "M8 right chance share is bounded [0,1]");
            Check(m8.SetPieceChanceShare >= 0 && m8.SetPieceChanceShare <= 1, "M8 set-piece chance share is bounded [0,1]");
            Check(Equal(m8.LeftChanceShare + m8.CentreChanceShare + m8.RightChanceShare + m8.SetPieceChanceShare, 1.0), "M8 chance shares sum to 1");
            Check(m8.StructuralChanceIndex >= 0 && m8.StructuralChanceIndex <= 1, "M8 structural chance index is bounded [0,1]");
            Check(m8.OwnRegularChanceExpected >= 0 && m8.OpponentRegularChanceExpected >= 0, "M8 regular chance volumes are non-negative");
            Check(m8.OpenChancePool == M8ChanceAllocationEngine.OpenChancePool, "M8 uses the production open-chance pool");

            Console.WriteLine($"M8 candidate={m8.CandidateId} | POS={m8.MidfieldShare:P2} | structural={m8.StructuralChanceIndex:P2} | own regular={m8.OwnRegularChanceExpected:F3} | opponent regular={m8.OpponentRegularChanceExpected:F3}");
            Console.WriteLine("PASS: C7 M8 chance model continuity + production recalculation");
            Console.WriteLine("NEXT: C8 M9 prediction");
            return 0;
        }
        catch (Exception ex) { return Fail("C7 exception: " + ex.Message); }
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
