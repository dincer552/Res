using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// M9 event -> goal regression against the 2026 paper Tables 4-5.
/// Appendix-C formula utilities are validated by dedicated unit coverage later.
/// </summary>
public static class M9EventGoalRegression
{
    public static int Run()
    {
        var players = new List<Player>
        {
            P(1, PlayerSpecialty.Quick), P(2, PlayerSpecialty.Technical), P(3, PlayerSpecialty.Head),
            P(4, PlayerSpecialty.Unpredictable), P(5, PlayerSpecialty.Unpredictable),
            P(6, PlayerSpecialty.None), P(7, PlayerSpecialty.None), P(8, PlayerSpecialty.None),
            P(9, PlayerSpecialty.None), P(10, PlayerSpecialty.None), P(11, PlayerSpecialty.None)
        };
        var slots = new[]
        {
            S("GK", 6), S("DEF-CL", 4), S("DEF-C", 3), S("DEF-CR", 7), S("DEF-L", 8), S("DEF-R", 9),
            S("IM-L", 10), S("IM-C", 11), S("IM-R", 2), S("W-L", 1), S("FW-C", 5)
        };

        var result = new M9EventGoalEngine().Calculate(
            new Lineup("Regression", "4-3-3", slots), players, 15, 15, AdvancedTactic.Normal, 1.0);

        Check(Math.Abs(result.ExpectedPlayerBasedEvents - 1.682) < 1e-12, "player event expectation", out var failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(result.ExpectedTeamBasedEvents - 0.93) < 1e-12, "team event expectation", out failure);
        if (failure is not null) return Fail(failure);
        Check(result.Contributions.Count == 13, "all 9 player + 4 team event classes represented", out failure);
        if (failure is not null) return Fail(failure);

        foreach (var expected in new Dictionary<string, double>
        {
            ["Winger"] = 0.4951, ["TechnicalOverHead"] = 0.2937, ["QuickRush"] = 0.3670,
            ["QuickPass"] = 0.4387, ["UnpredictableLongPass"] = 0.4090, ["UnpredictableScoreOwn"] = 0.5822,
            ["UnpredictableSpecialAction"] = 0.4241, ["UnpredictableMistake"] = 0.1816, ["UnpredictableOwnGoal"] = 0.1725,
        })
        {
            Check(Approximately(result, expected.Key, expected.Value), $"{expected.Key} conversion", out failure);
            if (failure is not null) return Fail(failure);
        }

        Console.WriteLine($"PASS: M9 event-goal regression | playerEvents={result.ExpectedPlayerBasedEvents:0.000} | teamEvents={result.ExpectedTeamBasedEvents:0.000}");
        return 0;
    }

    private static Player P(int id, PlayerSpecialty specialty) => new(id, $"P{id}", 1, 10, 10, 10, 10, 10, 10, 7, 7, 0, -1, specialty);
    private static Slot S(string code, int id) => new(code, code, string.Empty, $"P{id}", id, 0, 0, 0);
    private static bool Approximately(M9EventGoalBreakdown result, string name, double expected) => result.Contributions.Any(x => x.Event == name && Math.Abs(x.GoalProbability - expected) < 1e-12);
    private static void Check(bool condition, string message, out string? failure) => failure = condition ? null : message;
    private static int Fail(string message) { Console.WriteLine("FAIL: " + message); return 1; }
}
