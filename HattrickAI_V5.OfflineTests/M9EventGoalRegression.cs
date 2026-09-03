using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// M9 event -> goal regression against the 2026 paper Tables 4-5, Fig.16 and Appendix C.
/// Locks the documented event expectations plus PNF/PDIM resolution.
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
            P(6, PlayerSpecialty.Powerful),
            P(7, PlayerSpecialty.Powerful),
            P(8, PlayerSpecialty.None),
            P(9, PlayerSpecialty.None),
            P(10, PlayerSpecialty.None),
            P(11, PlayerSpecialty.None)
        };

        // Every documented Table-4 event remains eligible. P6 is the PNF and P7
        // is the PDIM; their positions deliberately exercise Appendix C.
        var slots = new[]
        {
            S("GK", 8), S("DEF-CL", 4), S("DEF-C", 3), S("DEF-CR", 9),
            S("DEF-L", 10), S("DEF-R", 11), S("IM-L", 7), S("IM-C", 2),
            S("W-L", 1), S("W-R", 5), S("FW-C", 6)
        };

        var lineup = new Lineup("Regression", "4-3-3", slots);
        var result = new M9EventGoalEngine().Calculate(
            lineup,
            players,
            ownMidfieldRating: 15,
            opponentMidfieldRating: 15,
            AdvancedTactic.Normal,
            creativeMultiplier: 1.0,
            ownNormalChanceVolume: 10,
            opponentNormalChanceVolume: 10,
            ownNormalGoalProbability: 0.5,
            opponentNormalGoalProbability: 0.5,
            opponentCentralDefenders: 3);

        Check(Math.Abs(result.ExpectedPlayerBasedEvents - 1.682) < 1e-12,
            "player-based event budget uses Binomial(4,0.841) x 50% ownership fallback", out var failure);
        if (failure is not null) return Fail(failure);

        Check(Math.Abs(result.ExpectedTeamBasedEvents - 0.93) < 1e-12,
            "equal midfield allocates half of Binomial(5,0.372) team-event expectation", out failure);
        if (failure is not null) return Fail(failure);

        Check(result.Contributions.Count == 15,
            "all 9 player-event + 4 team-event classes plus PNF/PDIM remain represented", out failure);
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

        Check(Math.Abs(M9EventGoalEngine.PnfConversionRate(1, 3) - 0.020) < 1e-12,
            "PNF=1 vs 3 CDs conversion = 2.00%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(M9EventGoalEngine.PnfConversionRate(2, 2) - 0.052) < 1e-12,
            "PNF=2 vs 2 CDs conversion = 5.20%", out failure);
        if (failure is not null) return Fail(failure);
        Check(Math.Abs(result.PressingSuppressionSignal - 0.065) < 1e-12,
            "one PDIM suppresses 6.5% of normal attacks", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "PowerfulNormalForward", 0.5), "PNF extra attacks resolve with own normal conversion", out failure);
        if (failure is not null) return Fail(failure);
        Check(Approximately(result, "PowerfulDefensiveInnerMidfielder", 0.0), "PDIM is a suppression signal, not a goal event", out failure);
        if (failure is not null) return Fail(failure);

        Console.WriteLine($"PASS: M9 event-goal regression | playerEvents={result.ExpectedPlayerBasedEvents:0.000} | teamEvents={result.ExpectedTeamBasedEvents:0.000} | PNF/PDIM enabled");
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
