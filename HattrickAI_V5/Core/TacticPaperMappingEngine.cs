namespace HattrickAI.V5.Core;

/// <summary>
/// Bridges the V5 internal tactical-strength scale to the RT scale used by
/// Constantinou et al. (2026) for Equation B.2. The RT conversion is kept
/// explicit so it can be replaced by an observed CHPP tactic-skill mapping
/// without changing M8's paper curves.
/// </summary>
public static class TacticPaperMappingEngine
{
    public const double V5InternalMax = 10.0;
    public const double PaperRtMax = 40.0;

    public static double ToPaperRt(double v5TacticalLevel)
        => Math.Clamp(v5TacticalLevel, 0.0, V5InternalMax) * (PaperRtMax / V5InternalMax);

    public static double PaperTacticConversionRate(AdvancedTactic tactic, double v5TacticalLevel)
        => M8ChanceAllocationEngine.CalculateTacticConversionRate(tactic, ToPaperRt(v5TacticalLevel));
}
