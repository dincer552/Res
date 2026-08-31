using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 refinement: keeps Motor 1 as the base and tests player swaps with
/// the real regional-rating engine against the opponent's real ratings.
/// RP is not used for the decision.
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
        var defence = own.LeftDefence - opponent.RightAttack
                    + own.CentralDefence - opponent.CentralAttack
                    + own.RightDefence - opponent.LeftAttack;
        var attack = own.LeftAttack - opponent.RightDefence
                   + own.CentralAttack - opponent.CentralDefence
                   + own.RightAttack - opponent.LeftDefence;
        var midfield = own.Midfield - opponent.Midfield;
        return 0.60 * defence + 0.70 * attack + 0.45 * midfield;
    }
}
