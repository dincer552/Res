using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

/// <summary>
/// Team-level individual-order optimizer.
///
/// Normal is the reference state. Candidate orders are evaluated against the
/// complete XI, not against isolated player ratings. Beam search is used so
/// interactions between several midfield orders are preserved instead of
/// relying on greedy coordinate ascent.
/// </summary>
public sealed class IndividualOrderOptimizer
{
    private readonly LineupRatingEngine _ratingEngine = new();

    private const int BeamWidth = 48;
    private const double TieTolerance = 0.000001;

    public Dictionary<int, PlayerBehaviour> Optimize(
        List<PlayerData> lineup,
        string formation,
        TeamMatchContext? context = null,
        TeamRatings? opponentRatings = null)
    {
        if (lineup == null || lineup.Count != 11)
            return new();

        context ??= new TeamMatchContext();
        var roles = LineupRatingEngine.GetRoles(formation);

        var normalBehaviours = Enumerable.Range(0, roles.Length)
            .ToDictionary(i => i, _ => PlayerBehaviour.Normal);

        var normalRatings = Calculate(lineup, formation, context, normalBehaviours);
        var initial = new SearchState(normalBehaviours, Score(normalRatings, normalRatings, opponentRatings));
        var beam = new List<SearchState> { initial };

        // Expand one slot at a time. Every valid Hattrick order is considered
        // for that position, while the beam keeps the strongest complete-team
        // combinations. This captures interactions between midfield orders.
        for (int slot = 0; slot < roles.Length; slot++)
        {
            var expanded = new List<SearchState>();
            foreach (var state in beam)
            {
                foreach (var candidate in BehavioursFor(roles[slot]))
                {
                    var next = new Dictionary<int, PlayerBehaviour>(state.Behaviours)
                    {
                        [slot] = candidate
                    };

                    var ratings = Calculate(lineup, formation, context, next);
                    double score = Score(ratings, normalRatings, opponentRatings);
                    expanded.Add(new SearchState(next, score));
                }
            }

            beam = expanded
                .OrderByDescending(x => x.Score)
                .ThenBy(x => SpecialOrderCount(x.Behaviours))
                .Take(BeamWidth)
                .ToList();
        }

        if (beam.Count == 0)
            return normalBehaviours;

        // Normal wins exact/near ties. This prevents gratuitous special orders.
        double bestScore = beam[0].Score;
        var tied = beam
            .Where(x => Math.Abs(x.Score - bestScore) <= TieTolerance)
            .OrderBy(x => SpecialOrderCount(x.Behaviours))
            .First();

        return new Dictionary<int, PlayerBehaviour>(tied.Behaviours);
    }

    private TeamRatings Calculate(
        List<PlayerData> lineup,
        string formation,
        TeamMatchContext context,
        IReadOnlyDictionary<int, PlayerBehaviour> behaviours)
    {
        return _ratingEngine.Calculate(
            lineup,
            formation,
            new TeamMatchContext
            {
                TacticType = context.TacticType,
                TacticLevel = context.TacticLevel,
                Attitude = context.Attitude,
                Location = context.Location,
                IsHome = context.IsHome,
                CoachModifier = context.CoachModifier,
                TeamSpirit = context.TeamSpirit,
                Confidence = context.Confidence,
                Weather = context.Weather,
                Minute = context.Minute,
                SlotBehaviours = behaviours
            });
    }

    private static double Score(
        TeamRatings ratings,
        TeamRatings normalRatings,
        TeamRatings? opponent)
    {
        double defence = (ratings.LeftDefence + ratings.CentralDefence + ratings.RightDefence) / 3.0;
        double attack = (ratings.LeftAttack + ratings.CentralAttack + ratings.RightAttack) / 3.0;

        double baseScore =
            ratings.Midfield * 1.30 +
            defence * 1.05 +
            attack * 1.10;

        // Protect the sectors that are being sacrificed. A special order is
        // only worthwhile when its gain is larger than the complete-team risk.
        baseScore -= SectorDropPenalty(ratings.Midfield, normalRatings.Midfield, 1.30);
        baseScore -= SectorDropPenalty(defence, AverageDefence(normalRatings), 1.05);
        baseScore -= SectorDropPenalty(attack, AverageAttack(normalRatings), 1.10);

        if (opponent != null)
        {
            // When opponent data exists, attack the weak side and protect
            // against the opponent's strongest attack without overwhelming the
            // underlying HO rating model.
            baseScore += MatchupBonus(ratings.LeftAttack, opponent.LeftDefence);
            baseScore += MatchupBonus(ratings.CentralAttack, opponent.CentralDefence);
            baseScore += MatchupBonus(ratings.RightAttack, opponent.RightDefence);

            baseScore += ProtectionBonus(ratings.LeftDefence, opponent.LeftAttack);
            baseScore += ProtectionBonus(ratings.CentralDefence, opponent.CentralAttack);
            baseScore += ProtectionBonus(ratings.RightDefence, opponent.RightAttack);
        }

        return baseScore;
    }

    private static double SectorDropPenalty(double current, double normal, double weight)
    {
        if (normal <= 0 || current >= normal * 0.985)
            return 0;

        double drop = (normal - current) / normal;
        return drop * drop * 20.0 * weight;
    }

    private static double MatchupBonus(double ownAttack, double opponentDefence)
    {
        if (ownAttack <= 0 || opponentDefence <= 0)
            return 0;

        // Small comparative term. The HO rating itself remains dominant.
        return Math.Clamp((ownAttack - opponentDefence) * 0.08, -0.60, 0.60);
    }

    private static double ProtectionBonus(double ownDefence, double opponentAttack)
    {
        if (ownDefence <= 0 || opponentAttack <= 0)
            return 0;

        return Math.Clamp((ownDefence - opponentAttack) * 0.06, -0.45, 0.45);
    }

    private static double AverageDefence(TeamRatings r)
        => (r.LeftDefence + r.CentralDefence + r.RightDefence) / 3.0;

    private static double AverageAttack(TeamRatings r)
        => (r.LeftAttack + r.CentralAttack + r.RightAttack) / 3.0;

    private static int SpecialOrderCount(IReadOnlyDictionary<int, PlayerBehaviour> behaviours)
        => behaviours.Count(x => x.Value != PlayerBehaviour.Normal);

    private static IReadOnlyList<PlayerBehaviour> BehavioursFor(PlayerRole role) => role switch
    {
        // Hattrick does not offer a defensive individual order to a central
        // defender.
        PlayerRole.CentralDefender => new[]
        {
            PlayerBehaviour.Normal,
            PlayerBehaviour.Offensive,
            PlayerBehaviour.TowardsWing
        },
        PlayerRole.LeftDefender or PlayerRole.RightDefender => new[]
        {
            PlayerBehaviour.Normal,
            PlayerBehaviour.Defensive,
            PlayerBehaviour.Offensive,
            PlayerBehaviour.TowardsMiddle
        },
        PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder => new[]
        {
            PlayerBehaviour.Normal,
            PlayerBehaviour.Defensive,
            PlayerBehaviour.Offensive,
            PlayerBehaviour.TowardsWing
        },
        PlayerRole.LeftWinger or PlayerRole.RightWinger => new[]
        {
            PlayerBehaviour.Normal,
            PlayerBehaviour.Defensive,
            PlayerBehaviour.Offensive,
            PlayerBehaviour.TowardsMiddle
        },
        PlayerRole.LeftForward or PlayerRole.RightForward or PlayerRole.CentralForward => new[]
        {
            PlayerBehaviour.Normal,
            PlayerBehaviour.Defensive,
            PlayerBehaviour.TowardsWing
        },
        _ => new[] { PlayerBehaviour.Normal }
    };

    private sealed record SearchState(
        Dictionary<int, PlayerBehaviour> Behaviours,
        double Score);
}
