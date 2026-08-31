using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 refinement: keeps Motor 1 as the base and tests player swaps with
/// the real regional-rating engine against the opponent's real ratings.
/// RP is not used for the decision.
///
/// The matchup score follows the structure of the Hattrick match engine more
/// closely than a simple rating-difference sum: midfield controls chance share
/// and each attack sector is compared with the opposite defence sector.
/// </summary>
public sealed class Motor2OpponentAwareRefiner
{
    private readonly RegionalRatingEngineFixed _ratings = new();
    private readonly PositionSuitabilityEngine _suitability = new();

    public Lineup Refine(Lineup initial, IReadOnlyList<Player> players, OpponentMatchProfile opponent)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(opponent);

        var best = initial;
        var bestScore = Score(best, players, opponent);

        // Motor 1 remains the base. Motor 2 only accepts a legal swap when the
        // complete regional-rating matchup improves, so RP cannot hijack XI selection.
        for (var pass = 0; pass < 3; pass++)
        {
            var changed = false;
            for (var a = 0; a < best.Slots.Count; a++)
            for (var b = a + 1; b < best.Slots.Count; b++)
            {
                var sa = best.Slots[a];
                var sb = best.Slots[b];
                if (sa.PlayerId <= 0 || sb.PlayerId <= 0) continue;

                var pa = players.FirstOrDefault(p => p.Id == sa.PlayerId);
                var pb = players.FirstOrDefault(p => p.Id == sb.PlayerId);
                if (pa is null || pb is null) continue;

                var ra = _suitability.Score(pb, sa.Code);
                var rb = _suitability.Score(pa, sb.Code);
                if (double.IsNegativeInfinity(ra) || double.IsNegativeInfinity(rb)) continue;

                var slots = best.Slots.ToArray();
                slots[a] = sa with { PlayerId = pb.Id, PlayerName = pb.Name, Rating = ra };
                slots[b] = sb with { PlayerId = pa.Id, PlayerName = pa.Name, Rating = rb };
                var candidate = new Lineup(best.TeamName, best.Formation, slots);
                var score = Score(candidate, players, opponent);

                if (score > bestScore + 0.0001)
                {
                    best = candidate;
                    bestScore = score;
                    changed = true;
                }
            }

            if (!changed) break;
        }

        return best;
    }

    private double Score(Lineup lineup, IReadOnlyList<Player> players, OpponentMatchProfile opponent)
    {
        var own = _ratings.CalculateLineup(lineup, players, RatingContext.Default);

        // Midfield chance share: M^3 / (M^3 + OppM^3).
        var ownMid = CubePositive(own.Midfield);
        var oppMid = CubePositive(opponent.Midfield);
        var totalMid = ownMid + oppMid;
        var ownChanceShare = totalMid <= 0.0 ? 0.5 : ownMid / totalMid;
        var oppChanceShare = 1.0 - ownChanceShare;

        // Hattrick regular chance distribution: 35% central, 25% each flank.
        var ownConversion =
            0.25 * GoalProbability(own.RightAttack, opponent.LeftDefence) +
            0.35 * GoalProbability(own.CentralAttack, opponent.CentralDefence) +
            0.25 * GoalProbability(own.LeftAttack, opponent.RightDefence);

        var opponentConversion =
            0.25 * GoalProbability(opponent.RightAttack, own.LeftDefence) +
            0.35 * GoalProbability(opponent.CentralAttack, own.CentralDefence) +
            0.25 * GoalProbability(opponent.LeftAttack, own.RightDefence);

        // Expected matchup advantage. This is intentionally used only to
        // compare legal swaps; the engine does not claim to predict final score.
        return ownChanceShare * ownConversion - oppChanceShare * opponentConversion;
    }

    private static double GoalProbability(double attack, double defence)
    {
        attack = Math.Max(0.0, attack);
        defence = Math.Max(0.0, defence);
        var a3 = CubePositive(attack);
        var d3 = CubePositive(defence);
        var denominator = 0.74 * a3 + d3;
        return denominator <= 0.0 ? 0.0 : (0.74 * a3) / denominator;
    }

    private static double CubePositive(double value)
    {
        var v = Math.Max(0.0, value);
        return v * v * v;
    }
}
