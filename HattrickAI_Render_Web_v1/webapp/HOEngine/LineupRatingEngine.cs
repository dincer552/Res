using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

public sealed class LineupRatingEngine
{
    private readonly PlayerRatingCalculator _calculator = new();
    private readonly RatingContributionTable _table = new();

    private static readonly Dictionary<RatingSector, double> SectorScale = new()
    {
        [RatingSector.Midfield] = .312,
        [RatingSector.LeftDefence] = .834,
        [RatingSector.RightDefence] = .834,
        [RatingSector.CentralDefence] = .501,
        [RatingSector.CentralAttack] = .513,
        [RatingSector.LeftAttack] = .615,
        [RatingSector.RightAttack] = .615
    };

    public TeamRatings Calculate(List<PlayerData> lineup, string formation)
        => Calculate(lineup, formation, new TeamMatchContext());

    public TeamRatings Calculate(List<PlayerData> lineup, string formation, TeamMatchContext context)
    {
        if (lineup == null || lineup.Count != 11)
            throw new ArgumentException("Lineup must contain eleven players.", nameof(lineup));

        var roles = GetRoles(formation);
        var behaviour = context.SlotBehaviours;

        double CalculateSector(RatingSector sector)
        {
            double raw = 0;
            var sectorCounts = roles
                .Select(ToLineupSector)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            for (int i = 0; i < lineup.Count; i++)
            {
                var role = roles[i];
                var lineupSector = ToLineupSector(role);
                var playerBehaviour = behaviour.TryGetValue(i, out var b)
                    ? b
                    : PlayerBehaviour.Normal;

                double contribution = _table.GetContribution(
                    lineup[i], role, sector, playerBehaviour, _calculator);

                if (contribution <= 0)
                    continue;

                double overcrowding = GetOvercrowdingPenalty(sectorCounts[lineupSector], lineupSector);
                contribution *= overcrowding;
                contribution += _calculator.ExperienceContribution(lineup[i].Experience, sector);
                contribution *= _calculator.WeatherFactor(lineup[i], context.Weather);
                contribution *= _calculator.StaminaFactor(lineup[i].Stamina, context.Minute, 0, context.TacticType);
                raw += contribution;
            }

            raw *= SectorContextFactor(sector, context);

            if (raw <= 0)
                return .75;

            return Math.Pow(raw * SectorScale[sector], 1.2) / 4.0 + 1.0;
        }

        return new TeamRatings(
            CalculateSector(RatingSector.Midfield),
            CalculateSector(RatingSector.LeftDefence),
            CalculateSector(RatingSector.CentralDefence),
            CalculateSector(RatingSector.RightDefence),
            CalculateSector(RatingSector.LeftAttack),
            CalculateSector(RatingSector.CentralAttack),
            CalculateSector(RatingSector.RightAttack));
    }

    public double GetPlayerPositionRating(PlayerData player, PlayerRole role, PlayerBehaviour behaviour, int minute = 0, int tacticType = 0)
    {
        double raw = 0;
        foreach (RatingSector sector in Enum.GetValues<RatingSector>())
        {
            double c = _table.GetContribution(player, role, sector, behaviour, _calculator);
            if (c <= 0)
                continue;

            c += _calculator.ExperienceContribution(player.Experience, sector);
            c *= _calculator.WeatherFactor(player, MatchWeather.Normal);
            c *= _calculator.StaminaFactor(player.Stamina, minute, 0, tacticType);
            c *= SectorScale[sector];
            if (sector == RatingSector.Midfield)
                c *= 3.0;
            raw += c;
        }

        return raw <= 0 ? 0 : Math.Pow(raw, 1.2) / 4.0;
    }

    public static PlayerRole[] GetRoles(string formation) => formation switch
    {
        "4-3-3" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender,
            PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder,
            PlayerRole.LeftForward, PlayerRole.CentralForward, PlayerRole.RightForward
        },
        "3-5-2" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender,
            PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger,
            PlayerRole.LeftForward, PlayerRole.CentralForward
        },
        "4-5-1" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender,
            PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger,
            PlayerRole.CentralForward
        },
        "5-4-1" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender,
            PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger,
            PlayerRole.CentralForward
        },
        "5-3-2" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender,
            PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder,
            PlayerRole.LeftForward, PlayerRole.CentralForward
        },
        "3-4-3" => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender,
            PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger,
            PlayerRole.LeftForward, PlayerRole.CentralForward, PlayerRole.RightForward
        },
        _ => new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender,
            PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger,
            PlayerRole.LeftForward, PlayerRole.CentralForward
        }
    };

    private static LineupContributionSector ToLineupSector(PlayerRole role) => role switch
    {
        PlayerRole.Goalkeeper => LineupContributionSector.Goal,
        PlayerRole.LeftDefender or PlayerRole.RightDefender => LineupContributionSector.Back,
        PlayerRole.CentralDefender => LineupContributionSector.CentralDefence,
        PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder => LineupContributionSector.InnerMidfield,
        PlayerRole.LeftWinger or PlayerRole.RightWinger => LineupContributionSector.Wing,
        PlayerRole.LeftForward or PlayerRole.CentralForward or PlayerRole.RightForward => LineupContributionSector.Forward,
        _ => LineupContributionSector.InnerMidfield
    };

    private static double GetOvercrowdingPenalty(int count, LineupContributionSector sector)
    {
        return sector switch
        {
            LineupContributionSector.CentralDefence => count switch { 2 => .964, 3 => .90, _ => 1.0 },
            LineupContributionSector.InnerMidfield => count switch { 2 => .935, 3 => .825, _ => 1.0 },
            LineupContributionSector.Forward => count switch { 2 => .945, 3 => .865, _ => 1.0 },
            _ => 1.0
        };
    }

    private static double SectorContextFactor(RatingSector sector, TeamMatchContext context)
    {
        double factor = 1.0;

        if (sector == RatingSector.Midfield)
        {
            if (context.Attitude == TeamAttitude.PIC) factor *= .83945;
            if (context.Attitude == TeamAttitude.MOTS) factor *= 1.1149;
            if (context.IsHome) factor *= 1.19892;
            if (context.TacticType == 2) factor *= .93;
            if (context.TacticType == 7) factor *= .96;
            if (context.TeamSpirit > 0) factor *= .1 + .425 * Math.Sqrt(context.TeamSpirit);
        }
        else if (sector is RatingSector.LeftDefence or RatingSector.RightDefence or RatingSector.CentralDefence)
        {
            factor *= CoachFactor(sector, context.CoachModifier);
            if (sector is RatingSector.LeftDefence or RatingSector.RightDefence)
            {
                if (context.TacticType == 3) factor *= .85;
                if (context.TacticType == 7) factor *= .93;
            }
            else
            {
                if (context.TacticType == 4) factor *= .85;
                if (context.TacticType == 7) factor *= .93;
            }
        }
        else
        {
            factor *= CoachFactor(sector, context.CoachModifier);
            if (context.TacticType == 7) factor *= .96;
            if (context.Confidence > 0) factor *= .8 + .05 * (context.Confidence + .5);
        }

        return factor;
    }

    private static double CoachFactor(RatingSector sector, int modifier)
    {
        if (modifier == 0) return 1.0;

        if (sector is RatingSector.LeftDefence or RatingSector.RightDefence or RatingSector.CentralDefence)
            return modifier <= 0
                ? 1.02 - modifier * (1.15 - 1.02) / 10.0
                : 1.02 - modifier * (1.02 - .90) / 10.0;

        return modifier <= 0
            ? 1.02 - modifier * (.90 - 1.02) / 10.0
            : 1.02 - modifier * (1.02 - 1.10) / 10.0;
    }

    private enum LineupContributionSector { Goal, CentralDefence, Back, InnerMidfield, Wing, Forward }
}
