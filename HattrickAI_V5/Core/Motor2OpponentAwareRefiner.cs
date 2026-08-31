using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 refinement layer. The initial XI comes from PositionSuitability;
/// this layer tests player swaps using the real regional-rating engine and the
/// opponent's real seven regional ratings. RP is intentionally not used.
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

        // Keep Motor 1 as the base and only accept swaps that improve the
        // opponent matchup. This prevents the tactical layer from replacing
        // a naturally suitable player just because of a small rating difference.
        for (var pass = 0; pass < 3; pass++)
        {
            var changed = false;
            for (var a = 0; a < best.Slots.Count; a++)
            for (var b = a + 1; b < best.Slots.Count; b++)
            {
                var sa = best.Slots[a];
                var sb = best.Slots[b];
                if (sa.PlayerId <= 0 || sb.PlayerId <= 0) continue;

                var candidateSlots = best.Slots.ToArray();
                candidateSlots[a] = sa with
                {
                    PlayerId = sb.PlayerId,
                    PlayerName = sb.PlayerName,
                    Rating = _suitability.Score(players.First(p => p.Id == sb.PlayerId), sa.Code)
                };
                candidateSlots[b] = sb with
                {
                    PlayerId = sa.PlayerId,
                    PlayerName = sa.PlayerName,
                    Rating = _suitability.Score(players.First(p => p.Id == sa.PlayerId), sb.Code)
                };

                if (candidateSlots.Any((s, i) => false)) continue;
                var candidate = new Lineup(best.TeamName, best.Formation, candidateSlots);
                if (!Valid(candidate, players)) continue;

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

    private bool Valid(Lineup lineup, IReadOnlyList<Player> players)
    {
        if (lineup.Slots.Select(s => s.PlayerId).Distinct().Count() != lineup.Slots.Count) return false;
        foreach (var s in lineup.Slots)
        {
            var p = players.FirstOrDefault(x => x.Id == s.PlayerId);
            if (p is null || double.IsNegativeInfinity(_suitability.Score(p, s.Code))) return false;
        }
        return true;
    }

    private double Score(Lineup lineup, IReadOnlyList<Player> players, OpponentMatchProfile opponent)
    {
        var own = _ratings.CalculateLineup(lineup, players, RatingContext.Default);
        var defence = (own.LeftDefence - opponent.RightAttack)
                    + (own.CentralDefence - opponent.CentralAttack)
                    + (own.RightDefence - opponent.LeftAttack);
        var attack = (own.LeftAttack - opponent.RightDefence)
                   + (own.CentralAttack - opponent.CentralDefence)
                   + (own.RightAttack - opponent.LeftDefence);
        var midfield = own.Midfield - opponent.Midfield;
        return 0.60 * defence + 0.70 * attack + 0.45 * midfield;
    }
}
