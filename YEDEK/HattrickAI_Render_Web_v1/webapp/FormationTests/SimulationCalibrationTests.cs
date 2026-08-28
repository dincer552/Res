using HattrickAI.HOEngine;

namespace HattrickAI.FormationTests;

internal static class SimulationCalibrationTests
{
    public static int RunAll()
    {
        var tests = new (string Name, System.Action Test)[]
        {
            ("equal attack/defence uses calibrated baseline", EqualAttackDefenceUsesCalibratedBaseline),
            ("attack advantage raises scoring probability without exceeding cap", AttackAdvantageIsBounded),
            ("75/25 midfield does not become 100% win", SeventyFiveTwentyFiveMidfieldIsNotAutomaticWin),
            ("balanced ratings keep average goals realistic", BalancedRatingsKeepGoalsRealistic)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Simulation calibration tests: {tests.Length - failures}/{tests.Length} passed.");
        return failures;
    }

    private static void EqualAttackDefenceUsesCalibratedBaseline()
    {
        var probability = GoalProbabilityModel.Calculate(10, 10);
        AssertClose(GoalProbabilityModel.EqualMatchProbability, probability, 0.000001,
            "Equal attack/defence should use the calibrated baseline");
        AssertTrue(probability < 0.25, "Equal ratings must not produce a 50%+ per-chance conversion rate");
    }

    private static void AttackAdvantageIsBounded()
    {
        var weaker = GoalProbabilityModel.Calculate(6, 12);
        var even = GoalProbabilityModel.Calculate(10, 10);
        var stronger = GoalProbabilityModel.Calculate(18, 6);

        AssertTrue(weaker < even, "Weaker attack should score less often");
        AssertTrue(stronger > even, "Stronger attack should score more often");
        AssertTrue(stronger <= GoalProbabilityModel.MaximumProbability, "Probability cap must be respected");
        AssertTrue(weaker >= GoalProbabilityModel.MinimumProbability, "Probability floor must be respected");
    }

    private static void SeventyFiveTwentyFiveMidfieldIsNotAutomaticWin()
    {
        var home = new TeamData(
            "Ev Sahibi",
            new TeamRatings(15, 10, 10, 10, 10, 10, 10),
            0,
            0);
        var away = new TeamData(
            "Deplasman",
            new TeamRatings(5, 10, 10, 10, 10, 10, 10),
            0,
            0);

        var result = new MatchSimulator(seed: 20260816).Run(home, away, 5000);

        AssertTrue(result.HomeWinPercentage < 95.0,
            $"75/25 midfield must not produce an almost-certain win: {result.HomeWinPercentage:F1}%");
        AssertTrue(result.AwayWinPercentage > 0.0,
            "The weaker midfield side must retain a non-zero upset probability");
        AssertTrue(result.DrawPercentage > 0.0,
            "The weaker midfield side must retain a non-zero draw probability");
    }

    private static void BalancedRatingsKeepGoalsRealistic()
    {
        var home = new TeamData(
            "Ev Sahibi",
            new TeamRatings(10, 10, 10, 10, 10, 10, 10),
            0,
            0);
        var away = new TeamData(
            "Deplasman",
            new TeamRatings(10, 10, 10, 10, 10, 10, 10),
            0,
            0);

        var result = new MatchSimulator(seed: 20260817).Run(home, away, 5000);
        var totalAverageGoals = result.AverageHomeGoals + result.AverageAwayGoals;

        AssertTrue(totalAverageGoals >= 1.0,
            $"Balanced matches should not collapse to near-zero scoring: {totalAverageGoals:F2}");
        AssertTrue(totalAverageGoals <= 4.0,
            $"Balanced matches should not explode into arcade scores: {totalAverageGoals:F2}");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}. Expected {expected:F6}, got {actual:F6}");
    }
}
