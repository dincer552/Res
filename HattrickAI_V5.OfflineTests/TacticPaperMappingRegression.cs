using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class TacticPaperMappingRegression
{
    public static int Run()
    {
        AssertNear(0.0, TacticPaperMappingEngine.ToPaperRt(0), "V5 0 -> RT 0");
        AssertNear(10.0, TacticPaperMappingEngine.ToPaperRt(5), "V5 5 -> RT 10");
        AssertNear(20.0, TacticPaperMappingEngine.ToPaperRt(10), "V5 10 -> RT 20");
        AssertNear(20.0, TacticPaperMappingEngine.ToPaperRt(999), "V5 upper clamp -> RT 20");
        AssertNear(0.0, TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.Normal, 5), "Normal has no TCR");

        AssertNear(
            M8ChanceAllocationEngine.CalculateTacticConversionRateFromPaperRt(AdvancedTactic.AttackMiddle, 20),
            TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.AttackMiddle, 10),
            "V5 10 -> paper RT 20 AiM");
        AssertNear(
            M8ChanceAllocationEngine.CalculateTacticConversionRateFromPaperRt(AdvancedTactic.AttackWings, 20),
            TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.AttackWings, 10),
            "V5 10 -> paper RT 20 AoW");
        AssertNear(
            M8ChanceAllocationEngine.CalculateTacticConversionRateFromPaperRt(AdvancedTactic.LongShots, 20),
            TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.LongShots, 10),
            "V5 10 -> paper RT 20 LS");
        AssertNear(
            M8ChanceAllocationEngine.CalculateTacticConversionRateFromPaperRt(AdvancedTactic.CounterAttack, 20),
            TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.CounterAttack, 10),
            "V5 10 -> paper RT 20 CA");

        Console.WriteLine("TacticPaperMappingRegression: PASS");
        return 0;
    }

    private static void AssertNear(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"TacticPaperMappingRegression failed: {message}; expected {expected}, actual {actual}");
    }
}
