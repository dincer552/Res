using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// M9 event -> goal regression against the 2026 paper Tables 4-5 and §4.4.
/// This locks the Binomial event expectations and prevents the player-event
/// ownership fallback from silently allocating the full match budget to one team.
/// </summary>
public static class M9EventGoalRegression
{
    public static int Run()
    {
        var players = new List<Player>
        {
            P(1, PlayerSpecialty.Quick),
            P(2, PlayerSpecialty.Technical),
            P(3, PlayerSpecialty.Head),
            P(4, PlayerSpecialty.Unpredictable),
            P(5, PlayerSpecialty.Unpredictable),
            P(6, PlayerSpecialty.None),
            P(7, PlayerSpecialty.None),
            P(8, PlayerSpecialty.None),
            P(9, PlayerSpecialty.None),
            P(10, PlayerSpecialty.None),
            P(11, PlayerSpecialty.None)
        };

        // Deliberately place specialties so every documented Table-4 event class
        // is eligible at least once: Quick/Technical on attack, Head on defence,
        // Unpredictable on defence + forward, and Winger on a wing.
        var slots = new[]
        {
            S("GK", 6), S("DEF-CL", 4), S("DEF-C", 3), S("DEF-CR", 7),
            S("DEF-L", 8), S("DEF-R", 9), S("IM-L", 10), S("IM-C", 11),
            S("IM-R", 2), S("W-L", 1), S("FW-C", 5)
        };

        var lineup = new Lineup("Regression", "4-3-3", slots);
        var result = new M9EventGoalEngine().Calculate(
            lineup,
            players,
            ownMidfieldRating: 15,
            opponentMidfieldRating: 15,
            AdvancedTactic.Normal,
            creativeMultiplier: 1.0);

        Check(Math.Abs(result.ExpectedPlayerBasedEvents - 1.682) < 1e-12,
            "player-based event budget uses Binomial(4,0.841) x 50% ownership fallback", out var failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(result.ExpectedTeamBasedEvents - 0.93) < 1e-12,
            "equal midfield allocates half of Binomial(5,0.372) team-event expectation", out failure);
        if (failure is not null) return Fail(failure);

        Check(result.Contributions.Count == 13,
            "all 9 player-event types and 4 team-event types remain represented", out failure);
        if (failure is not null) return Fail(failure);

        Check(Approximately(result, "Winger", 0.4951), "Winger goal conversion = 49.51%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "TechnicalOverHead", 0.2937), "Technical over Head goal conversion = 29.37%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "QuickRush", 0.3670), "Quick Rush goal conversion = 36.70%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "QuickPass", 0.4387), "Quick Pass goal conversion = 43.87%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "UnpredictableLongPass", 0.4090), "Unpredictable long pass goal conversion = 40.90%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "UnpredictableScoreOwn", 0.5822), "Unpredictable solo score conversion = 58.22%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "UnpredictableSpecialAction", 0.4241), "Unpredictable special action conversion = 42.41%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "UnpredictableMistake", 0.1816), "Unpredictable mistake conversion = 18.16%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "UnpredictableOwnGoal", 0.1725), "Unpredictable own-goal probability = 17.25%", out failure);
        if (failure is not null) return Fail(failure);

        Console.WriteLine($"PASS: M9 event-goal regression | playerEvents={result.ExpectedPlayerBasedEvents:0.000} | teamEvents={result.ExpectedTeamBasedEvents:0.000}");
        return 0;
    }

    private static Player P(int id, PlayerSpecialty specialty)
        => new(id, $"P{id}", 1, 10, 10, 10, 10, 10, 10, 7, 7, 0, -1, specialty);

    private static Slot S(string code, int id)
        => new(code, code, string.Empty, $"P{id}", id, 0, 0, 0);

    private static bool Approximately(M9EventGoalBreakdown result, string eventName, double expected)
        => result.Contributions.Any(x => x.Event == eventName && Math.Abs(x.GoalProbability - expected) < 1e-12);

    private static void Check(bool condition, string message, out string? failure)
        => failure = condition ? null : message;

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }
}
