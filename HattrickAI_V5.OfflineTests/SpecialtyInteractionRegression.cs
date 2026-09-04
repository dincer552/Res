using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class SpecialtyInteractionRegression
{
    public static int Run()
    {
        AssertNear(1.05, SpecialtyInteractionEngine.GetWeatherEffect(PlayerSpecialty.Technical, MatchWeather.Sun).ScoringMultiplier, "Technical sun scoring");
        AssertNear(0.95, SpecialtyInteractionEngine.GetWeatherEffect(PlayerSpecialty.Technical, MatchWeather.Rain).PlaymakingMultiplier, "Technical rain playmaking");
        AssertNear(1.05, SpecialtyInteractionEngine.GetWeatherEffect(PlayerSpecialty.Powerful, MatchWeather.Rain).DefendingMultiplier, "Powerful rain defending");
        AssertNear(0.95, SpecialtyInteractionEngine.GetWeatherEffect(PlayerSpecialty.Quick, MatchWeather.Sun).DefendingMultiplier, "Quick sun defending");
        Assert(SpecialtyInteractionEngine.ApplyWeatherToSkill(10, PlayerSpecialty.Technical, MatchWeather.Sun, x => x.ScoringMultiplier) == 11, "10 -> 11 Technical sun");
        Assert(SpecialtyInteractionEngine.ApplyWeatherToSkill(10, PlayerSpecialty.Quick, MatchWeather.Rain, x => x.ScoringMultiplier) == 10, "10 remains 10 after symmetric rounding");
        AssertNear(0.05, SpecialtyInteractionEngine.CounterAttackSpecialtyBoostPercent(1, 0), "1 Quick CA boost");
        AssertNear(0.14, SpecialtyInteractionEngine.CounterAttackSpecialtyBoostPercent(8, 0), "8 Quick CA boost");
        AssertNear(0.0, SpecialtyInteractionEngine.CounterAttackSpecialtyBoostPercent(8, 8), "8 opponent Quicks nullify extra boost");
        AssertNear(1.33, SpecialtyInteractionEngine.GetTacticEffect(PlayerSpecialty.Technical, "FW-C", PlayerOrder.Defensive, AdvancedTactic.Normal).TechnicalDefensiveWingPassingMultiplier, "Technical defensive forward passing");
        AssertNear(2.0, SpecialtyInteractionEngine.GetTacticEffect(PlayerSpecialty.Powerful, "DEF-C", PlayerOrder.Normal, AdvancedTactic.Pressing).PressingDefenceWeightMultiplier, "Powerful pressing defence weight");
        Assert(SpecialtyInteractionEngine.GetTacticEffect(PlayerSpecialty.Quick, "FW-C", PlayerOrder.Normal, AdvancedTactic.CounterAttack, 1, 0).CanCreateQuickEvent, "Quick forward event");
        Assert(SpecialtyInteractionEngine.GetTacticEffect(PlayerSpecialty.Technical, "DEF-C", PlayerOrder.Normal, AdvancedTactic.Normal).CanCreateNonTacticalCounterAttack, "Technical defender non-tactical CA");
        Assert(SpecialtyInteractionEngine.GetTacticEffect(PlayerSpecialty.Unpredictable, "IM-C", PlayerOrder.Normal, AdvancedTactic.Creative).IsSpecialEventAmplified, "Creative special-event amplification");
        AssertNear(0.21, SpecialtyInteractionEngine.HeadSetPieceScoringOpportunityBonus(3), "3 offensive headers");
        AssertNear(0.15, SpecialtyInteractionEngine.OpponentHeadSetPieceSuppression(3), "3 defensive headers");

        Console.WriteLine("SpecialtyInteractionRegression: PASS");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"SpecialtyInteractionRegression failed: {message}");
    }

    private static void AssertNear(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"SpecialtyInteractionRegression failed: {message}; expected {expected}, actual {actual}");
    }
}
