using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.FormationTests;

internal static class RecommendationEngineTests
{
    public static int RunAll()
    {
        try
        {
            TestTrainingFormationMatrix();
            TestHardTrainingPriority();
            TestAutomaticRecommendationRespectsTrainingTier();
            TestForcedFormationStillWorks();
            TestInsufficientSquadRejected();
            TestLatestCupFixtureSelectionUsesCompletedCupMatch();
            TestLatestCupFixtureSelectionDoesNotDependOnStatusText();
            Console.WriteLine("PASS recommendation/cup regression tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL recommendation/cup regression tests: {ex.Message}");
            return 1;
        }
    }

    private static void TestTrainingFormationMatrix()
    {
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(4, "3-4-3"), "Scoring 3-4-3 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(4, "4-3-3"), "Scoring 4-3-3 must be full fit");
        AssertNear(0.70, RecommendationEngine.TrainingFormationFit(4, "4-4-2"), "Scoring 4-4-2 must be partial fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(8, "3-5-2"), "Playmaking 3-5-2 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(8, "4-5-1"), "Playmaking 4-5-1 must be full fit");
        AssertNear(0.70, RecommendationEngine.TrainingFormationFit(8, "3-4-3"), "Playmaking 3-4-3 must be partial fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(3, "5-4-1"), "Defending 5-4-1 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(3, "5-3-2"), "Defending 5-3-2 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(7, "3-4-3"), "Short passes 3-4-3 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(7, "3-5-2"), "Short passes 3-5-2 must be full fit");
        AssertNear(0.90, RecommendationEngine.TrainingFormationFit(7, "4-4-2"), "Short passes 4-4-2 must be partial fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(10, "5-4-1"), "Through passes 5-4-1 must be full fit");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(10, "4-5-1"), "Through passes 4-5-1 must be full fit");
        AssertNear(0.80, RecommendationEngine.TrainingFormationFit(0, "4-4-2"), "General must be formation-flexible");
        AssertNear(0.80, RecommendationEngine.TrainingFormationFit(1, "3-5-2"), "Stamina must be formation-flexible");
        AssertNear(0.80, RecommendationEngine.TrainingFormationFit(2, "5-4-1"), "Set pieces must be formation-flexible");
        AssertNear(0.80, RecommendationEngine.TrainingFormationFit(9, "4-3-3"), "Goalkeeping must be formation-flexible");
    }

    private static void TestHardTrainingPriority()
    {
        var engine = new RecommendationEngine();
        var players = BuildSquad();
        var opponent = BuildOpponent();
        var full = engine.RecommendForFormation(players, opponent, "3-4-3", 100, true, trainingType: 4);
        var poor = engine.RecommendForFormation(players, opponent, "4-4-2", 100, true, trainingType: 4);
        AssertTrue(full != null, "Full-fit formation must produce a recommendation");
        AssertTrue(poor != null, "Low-fit formation must produce a recommendation");
        AssertTrue(full!.TrainingFit == 2, "Full-fit recommendation must be marked as tier 2");
        AssertTrue(poor!.TrainingFit == 0, "4-4-2 scoring fit must be tier 0");
        AssertTrue(full.SelectionScore > poor.SelectionScore, "Full training tier must beat low training tier even when match simulation is evaluated");
    }

    private static void TestAutomaticRecommendationRespectsTrainingTier()
    {
        var result = new RecommendationEngine().Recommend(BuildSquad(), BuildOpponent(), 100, true, trainingType: 4);
        AssertTrue(result != null, "Automatic recommendation must produce a result");
        AssertNear(1.00, RecommendationEngine.TrainingFormationFit(4, result!.Formation), "Automatic scoring recommendation must stay inside the full-training tier");
        AssertTrue(result.TrainingFit == 2, "Automatic recommendation must report full training tier");
        AssertTrue(result.TrainingPriority == "Tam antrenman uyumu", "Automatic recommendation must report the training priority correctly");
    }

    private static void TestForcedFormationStillWorks()
    {
        var opponent = BuildOpponent();
        opponent.PreferredFormation = "3-4-3";
        var result = new RecommendationEngine().Recommend(BuildSquad(), opponent, 100, true, trainingType: 8);
        AssertTrue(result != null, "Forced formation must still produce a recommendation");
        AssertTrue(result!.Formation == "3-4-3", "UI-selected formation must remain forced");
    }

    private static void TestInsufficientSquadRejected()
    {
        var result = new RecommendationEngine().Recommend(BuildSquad().Take(10).ToList(), BuildOpponent(), 100, true, trainingType: 4);
        AssertTrue(result == null, "A squad with fewer than 11 players must be rejected");
    }

    private static void TestLatestCupFixtureSelectionUsesCompletedCupMatch()
    {
        var now = DateTime.Now;
        var fixtures = new[]
        {
            new ChppFixture(100, now.AddDays(-3), 1, "FINISHED", 7, "Own", 8, "League Opp", 2, 0),
            new ChppFixture(200, now.AddDays(-2), 3, "FINISHED", 7, "Own", 9, "Old Cup", 1, 0),
            new ChppFixture(300, now.AddDays(-1), 3, "FINISHED", 10, "Other", 7, "Own", 0, 2)
        };
        var selected = ChppMatchDataService.SelectLatestStandardCupFixture(fixtures, 7);
        AssertTrue(selected?.MatchId == 300, "Latest completed cup match must be selected regardless of home/away side");
    }

    private static void TestLatestCupFixtureSelectionDoesNotDependOnStatusText()
    {
        var fixture = new ChppFixture(400, DateTime.Now.AddHours(-2), 3, "", 7, "Own", 11, "Cup Opp", 3, 1);
        var selected = ChppMatchDataService.SelectLatestStandardCupFixture(new[] { fixture }, 7);
        AssertTrue(selected?.MatchId == 400, "Completed cup selection must use score/date and not require a specific Status string");
    }

    private static List<PlayerData> BuildSquad()
    {
        var players = new List<PlayerData>();
        for (var i = 1; i <= 16; i++)
        {
            players.Add(new PlayerData
            {
                PlayerId = i, Name = $"Test Player {i}", Age = 22, Form = 8, Stamina = 8, Experience = 8,
                Keeper = 7, Defending = 8, Playmaking = 8, Winger = 8, Passing = 8, Scoring = 8, SetPieces = 8
            });
        }
        return players;
    }

    private static TeamData BuildOpponent() => new("Test Opponent", new TeamRatings(8, 8, 8, 8, 8, 8, 8), 0, 0);

    private static void AssertNear(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.0001) throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
