namespace HattrickAI.V5.Core;

/// <summary>
/// Applies the team's current confidence to attack ratings.
/// CHPP exposes SelfConfidence as: 4 = decent, 5 = strong, 6 = wonderful,
/// 7 = slightly exaggerated, etc. Hattrick documents that confidence is
/// reflected in attack-sector ratings; the exact per-level coefficient is
/// empirical, so V5 uses the conservative 5% per confidence level around 4.
/// </summary>
public static class ConfidenceRatingAdjuster
{
    private const double NeutralConfidence = 4.0;
    private const double AttackPerLevel = 0.05;

    public static RegionalRatingSnapshot Apply(RegionalRatingSnapshot rating, int confidenceLevel)
    {
        var level = Math.Clamp(confidenceLevel, 0, 9);
        var multiplier = 1.0 + (level - NeutralConfidence) * AttackPerLevel;
        multiplier = Math.Clamp(multiplier, 0.80, 1.25);

        return Rebuild(
            rating,
            rating.RawLeftDefence,
            rating.RawCentralDefence,
            rating.RawRightDefence,
            rating.RawMidfield,
            rating.RawLeftAttack * multiplier,
            rating.RawCentralAttack * multiplier,
            rating.RawRightAttack * multiplier);
    }

    private static RegionalRatingSnapshot Rebuild(
        RegionalRatingSnapshot r,
        double ld, double cd, double rd, double mid,
        double la, double ca, double ra)
        => new(
            ld, cd, rd, mid, la, ca, ra,
            RegionalRatingEngine.Display(ld),
            RegionalRatingEngine.Display(cd),
            RegionalRatingEngine.Display(rd),
            RegionalRatingEngine.Display(mid),
            RegionalRatingEngine.Display(la),
            RegionalRatingEngine.Display(ca),
            RegionalRatingEngine.Display(ra));
}
