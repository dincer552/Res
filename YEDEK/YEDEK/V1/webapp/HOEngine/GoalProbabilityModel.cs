using System;

namespace HattrickAI.HOEngine;

/// <summary>
/// Converts an attack-vs-defence rating comparison into a per-chance scoring
/// probability. The old HO effectiveness curve is intentionally not used here:
/// it turns a 50/50 attack-vs-defence comparison into a 50% scoring chance,
/// which is far too high when applied to every simulated chance.
/// </summary>
public static class GoalProbabilityModel
{
    public const double EqualMatchProbability = 0.16;
    public const double MinimumProbability = 0.05;
    public const double MaximumProbability = 0.36;

    public static double Calculate(double attackRating, double defenceRating)
    {
        attackRating = Math.Max(0d, attackRating);
        defenceRating = Math.Max(0d, defenceRating);

        if (attackRating + defenceRating <= 0.000001d)
            return EqualMatchProbability;

        double attackShare = attackRating / (attackRating + defenceRating);
        double relativeAdvantage = (attackShare - 0.5d) * 2d;

        // 50/50 = 16%; a clear advantage increases the chance gradually,
        // while the hard bounds prevent ratings from producing arcade-like
        // 50-100% conversion rates.
        double probability = EqualMatchProbability + (0.20d * relativeAdvantage);
        return Math.Clamp(probability, MinimumProbability, MaximumProbability);
    }
}
