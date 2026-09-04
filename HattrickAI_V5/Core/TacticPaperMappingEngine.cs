namespace HattrickAI.V5.Core;

/// <summary>
/// Bridges the V5 internal tactical-strength scale (0-10) to the tactic-rating
/// scale used by the 2026 Hattrick paper's Equation B.2.
///
/// V5 deliberately keeps its compact 0-10 internal scale. The paper examples
/// and current Hattrick tactic-skill scale use 1-20, so the explicit bridge is
/// RT = V5 * 2, clamped to 0-20. M8 keeps the paper equations unchanged.
/// </summary>
public static class TacticPaperMappingEngine
{
    public const double V5InternalMax = 10.0;
    public const double PaperRtMax = 20.0;
    public const double PaperRtPerV5Level = PaperRtMax / V5InternalMax;

    public static double ToPaperRt(double v5TacticalLevel)
        => Math.Clamp(v5TacticalLevel, 0.0, V5InternalMax) * PaperRtPerV5Level;

    public static double PaperTacticConversionRate(AdvancedTactic tactic, double v5TacticalLevel)
        => M8ChanceAllocationEngine.CalculateTacticConversionRateFromPaperRt(tactic, ToPaperRt(v5TacticalLevel));
}
