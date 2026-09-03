namespace HattrickAI.V5.Core;

// Compatibility aliases for the PDF tactic constants used by the tactical scenario layer.
// Canonical values remain owned by M8ChanceAllocationEngine.
public static class PdfTacticalAliases
{
    public const double PdfAiMMin = M8ChanceAllocationEngine.AiMMinWingConversion;
    public const double PdfAiMMax = M8ChanceAllocationEngine.AiMMaxWingConversion;
    public const double PdfAoWMin = M8ChanceAllocationEngine.AoWMinCentreConversion;
    public const double PdfAoWMax = M8ChanceAllocationEngine.AoWMaxCentreConversion;
    public const double PdfCaMin = M8ChanceAllocationEngine.CounterAttackMinConversion;
    public const double PdfCaMax = M8ChanceAllocationEngine.CounterAttackMaxConversion;
    public const double PdfLongShotsMin = M8ChanceAllocationEngine.LongShotsMinConversion;
    public const double PdfLongShotsMax = M8ChanceAllocationEngine.LongShotsMaxConversion;
    public const double PdfPressingMin = M8ChanceAllocationEngine.PressingMinSuppression;
    public const double PdfPressingMax = M8ChanceAllocationEngine.PressingMaxSuppression;
}
