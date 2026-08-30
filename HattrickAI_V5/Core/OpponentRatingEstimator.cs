using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// Estimates a comparable 0-20 player RP for opponents whose player skills are
/// not available through CHPP. The estimator deliberately relies only on data
/// available for the historical match: player stars, post-reposition position,
/// behaviour and the team's seven real sector ratings.
/// </summary>
public static class OpponentRatingEstimator
{
    public static double Estimate(
        double historicalStars,
        string slotCode,
        int behaviour,
        RegionalRatingSnapshot teamRating,
        int teamExperienceLevel)
    {
        // Stars are already an individual match-performance measure. Using a
        // straight 2x conversion puts them onto the same 0-20 scale used by
        // our own player RP without collapsing the opponent values to 0-2.
        var baseRp = Math.Clamp(historicalStars, 0, 10) * 2.0;
        if (baseRp <= 0)
            return 0;

        var context = PositionalContext(slotCode, teamRating);

        // Small team-strength adjustment only. The historical star value must
        // remain the dominant signal, while the real sector ratings still
        // influence the estimate in a controlled range of roughly +/- 4%.
        var contextFactor = 1.0 + 0.04 * Math.Clamp((context - 8.0) / 4.0, -1.0, 1.0);

        // Experience level is a team-level stabilizer, never a replacement for
        // the player's observed stars. The effect is intentionally tiny.
        var experienceFactor = 1.0 + 0.0025 * Math.Clamp(teamExperienceLevel - 6, -4, 4);

        // Behaviour is kept as a secondary modifier because PositionCode already
        // represents the player's final position after repositioning.
        var behaviourFactor = BehaviourFactor(behaviour, slotCode);

        return Math.Round(
            Math.Clamp(baseRp * contextFactor * experienceFactor * behaviourFactor, 0, 20),
            2);
    }

    private static double BehaviourFactor(int behaviour, string slotCode)
    {
        var isDefensive = slotCode == "GK" || slotCode.StartsWith("DEF", StringComparison.Ordinal);
        var isMidfield = slotCode.StartsWith("IM", StringComparison.Ordinal);
        var isWide = slotCode.StartsWith("W-", StringComparison.Ordinal);
        var isAttack = slotCode.StartsWith("FW", StringComparison.Ordinal);

        return behaviour switch
        {
            1 => isAttack || isWide ? 1.020 : isDefensive ? 0.985 : 1.010, // Offensive
            2 => isDefensive ? 1.020 : isAttack ? 0.985 : 1.010,            // Defensive
            3 => isMidfield || isAttack ? 1.015 : isWide ? 0.990 : 1.000,   // Towards middle
            4 => isWide || isAttack ? 1.015 : isMidfield ? 1.000 : 0.990,    // Towards wing
            5 => 1.015,                                                        // Extra forward
            6 => 1.015,                                                        // Extra inner midfield
            7 => isDefensive ? 1.015 : 0.995,                                  // Extra defender
            _ => 1.000
        };
    }

    private static double PositionalContext(string slotCode, RegionalRatingSnapshot r)
    {
        var avgDef = (r.LeftDefence + r.CentralDefence + r.RightDefence) / 3.0;
        var avgAtt = (r.LeftAttack + r.CentralAttack + r.RightAttack) / 3.0;

        if (slotCode == "GK")
            return 0.20 * r.LeftDefence + 0.60 * r.CentralDefence + 0.20 * r.RightDefence;

        if (slotCode == "DEF-CL")
            return 0.18 * r.LeftDefence + 0.64 * r.CentralDefence + 0.18 * avgDef;

        if (slotCode == "DEF-CR")
            return 0.18 * r.RightDefence + 0.64 * r.CentralDefence + 0.18 * avgDef;

        if (slotCode == "DEF-L")
            return 0.65 * r.LeftDefence + 0.25 * r.CentralDefence + 0.10 * r.LeftAttack;

        if (slotCode == "DEF-R")
            return 0.65 * r.RightDefence + 0.25 * r.CentralDefence + 0.10 * r.RightAttack;

        if (slotCode == "W-L")
            return 0.20 * r.LeftDefence + 0.20 * r.Midfield + 0.45 * r.LeftAttack + 0.15 * r.CentralAttack;

        if (slotCode == "W-R")
            return 0.20 * r.RightDefence + 0.20 * r.Midfield + 0.45 * r.RightAttack + 0.15 * r.CentralAttack;

        if (slotCode == "IM-L")
            return 0.10 * r.LeftDefence + 0.55 * r.Midfield + 0.20 * r.LeftAttack + 0.15 * r.CentralAttack;

        if (slotCode == "IM-R")
            return 0.10 * r.RightDefence + 0.55 * r.Midfield + 0.20 * r.RightAttack + 0.15 * r.CentralAttack;

        if (slotCode == "IM-C")
            return 0.70 * r.Midfield + 0.15 * r.CentralDefence + 0.15 * r.CentralAttack;

        if (slotCode == "FW-L")
            return 0.15 * r.Midfield + 0.20 * r.LeftAttack + 0.65 * r.CentralAttack;

        if (slotCode == "FW-R")
            return 0.15 * r.Midfield + 0.20 * r.RightAttack + 0.65 * r.CentralAttack;

        return (avgDef + r.Midfield + avgAtt) / 3.0;
    }
}
