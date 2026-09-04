using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class TacticPaperMappingRegression
{
    public static int Run()
    {
        AssertNear(0.0, TacticPaperMappingEngine.ToPaperRt(0), "V5 0 -> RT 0");
        AssertNear(20.0, TacticPaperMappingEngine.ToPaperRt(5), "V5 5 -> RT 20");
        AssertNear(40.0, TacticPaperMappingEngine.ToPaperRt(10), "V5 10 -> RT 40");
        AssertNear(0.0, TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.Normal, 5), "Normal has no TCR");

        AssertNear(M8ChanceAllocationEngine.CalculateTacticConversionRate(AdvancedTactic.AttackMiddle, 20), TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.AttackMiddle, 5), "V5 5 -> paper RT 20 AiM");
        AssertNear(M8ChanceAllocationEngine.CalculateTacticConversionRate(AdvancedTactic.AttackWings, 20), TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.AttackWings, 5), "V5 5 -> paper RT 20 AoW");
        AssertNear(M8ChanceAllocationEngine.CalculateTacticConversionRate(AdvancedTactic.LongShots, 20), TacticPaperMappingEngine.PaperTacticConversionRate(AdvancedTactic.LongShots, 5), "V5 5 -> paper RT 20 LS");

        Console.WriteLine("TacticPaperMappingRegression: PASS");
        return 0;
    }

    private static void AssertNear(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"TacticPaperMappingRegression failed: {message}; expected {expected}, actual {actual}");
    }
}
