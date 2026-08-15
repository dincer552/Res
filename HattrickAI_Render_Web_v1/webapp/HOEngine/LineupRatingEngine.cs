using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

/// <summary>
/// Direct C# port of HO!'s RatingPredictionModel sector-rating path.
/// The order is intentionally the same as HO:
/// contribution -> overcrowding -> experience -> weather -> stamina -> sector context -> scale.
/// No custom rating normalization is applied.
/// </summary>
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
        var behaviours = context.SlotBehaviours;
        var sectorCounts = roles.Select(ToLineupSector).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        double CalculateSector(RatingSector sector)
        {
            double ret = 0;
            for (int i = 0; i < lineup.Count; i++)
            {
                var player = lineup[i];
                var role = roles[i];
                var behaviour = behaviours.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal;
                var lineupSector = ToLineupSector(role);
                var overcrowding = GetOvercrowdingPenalty(sectorCounts[lineupSector], lineupSector);

                var contribution = _table.GetContribution(player, role, sector, behaviour, _calculator);
                if (contribution <= 0)
                    continue;

                contribution *= overcrowding;
                contribution += _calculator.ExperienceContribution(player.Experience, sector);
                contribution *= _calculator.WeatherFactor(player, context.Weather);
                contribution *= _calculator.StaminaFactor(player.Stamina, context.Minute, 0, context.TacticType);
                ret += contribution;
            }

            ret *= CalcSector(sector, context);
            return ScaleSector(sector, ret);
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
        double ret = 0;
        foreach (RatingSector sector in Enum.GetValues<RatingSector>())
        {
            var contribution = _table.GetContribution(player, role, sector, behaviour, _calculator);
            if (contribution <= 0)
                continue;

            contribution += _calculator.ExperienceContribution(player.Experience, sector);
            contribution *= _calculator.WeatherFactor(player, MatchWeather.Normal);
            contribution *= _calculator.StaminaFactor(player.Stamina, minute, 0, tacticType);
            contribution *= SectorScale[sector];
            if (sector == RatingSector.Midfield)
                contribution *= 3;
            ret += contribution;
        }
        return ret > 0 ? Math.Pow(ret, 1.2) / 4.0 : 0;
    }

    private static double ScaleSector(RatingSector sector, double ret)
        => ret > 0 ? Math.Pow(ret * SectorScale[sector], 1.2) / 4.0 + 1.0 : .75;

    private static double CalcSector(RatingSector sector, TeamMatchContext context)
    {
        double r = 1.0;
        var location = context.IsHome ? TeamLocation.Home : context.Location;

        switch (sector)
        {
            case RatingSector.Midfield:
                if (context.Attitude == TeamAttitude.PIC) r *= 0.83945;
                else if (context.Attitude == TeamAttitude.MOTS) r *= 1.1149;

                if (location == TeamLocation.AwayDerby) r *= 1.11493;
                else if (location == TeamLocation.Home) r *= 1.19892;

                if (context.TacticType == 2) r *= 0.93;
                else if (context.TacticType == 5) r *= 0.96;
                r *= CalcTeamSpirit(context.TeamSpirit);
                break;

            case RatingSector.LeftDefence:
            case RatingSector.RightDefence:
                r *= CoachFactor(sector, context.CoachModifier);
                if (context.TacticType == 3) r *= 0.85;
                else if (context.TacticType == 7) r *= 0.93;
                break;

            case RatingSector.CentralDefence:
                r *= CoachFactor(sector, context.CoachModifier);
                if (context.TacticType == 4) r *= 0.85;
                else if (context.TacticType == 7) r *= 0.93;
                break;

            case RatingSector.CentralAttack:
            case RatingSector.LeftAttack:
            case RatingSector.RightAttack:
                r *= CoachFactor(sector, context.CoachModifier);
                if (context.TacticType == 5) r *= 0.96;
                r *= CalcConfidence(context.Confidence);
                break;
        }
        return r;
    }

    private static double CalcConfidence(double confidence) => 0.8 + 0.05 * (confidence + .5);
    private static double CalcTeamSpirit(double teamSpirit) => 0.1 + 0.425 * Math.Sqrt(Math.Max(0, teamSpirit));

    private static double CoachFactor(RatingSector sector, int modifier)
    {
        if (sector is RatingSector.LeftDefence or RatingSector.RightDefence or RatingSector.CentralDefence)
            return modifier <= 0 ? 1.02 - modifier * (1.15 - 1.02) / 10.0 : 1.02 - modifier * (1.02 - .90) / 10.0;
        return modifier <= 0 ? 1.02 - modifier * (.90 - 1.02) / 10.0 : 1.02 - modifier * (1.02 - 1.10) / 10.0;
    }

    public static PlayerRole[] GetRoles(string formation) => formation switch
    {
        "4-4-2" => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.LeftForward, PlayerRole.CentralForward },
        "4-3-3" => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.LeftForward, PlayerRole.CentralForward, PlayerRole.RightForward },
        "3-5-2" => new[] { PlayerRole.Goalkeeper, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.LeftForward, PlayerRole.CentralForward },
        "4-5-1" => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.CentralForward },
        "5-4-1" => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.CentralForward },
        "5-3-2" => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.LeftForward, PlayerRole.CentralForward },
        "3-4-3" => new[] { PlayerRole.Goalkeeper, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.LeftForward, PlayerRole.CentralForward, PlayerRole.RightForward },
        _ => new[] { PlayerRole.Goalkeeper, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender, PlayerRole.LeftWinger, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightWinger, PlayerRole.LeftForward, PlayerRole.CentralForward }
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

    private static double GetOvercrowdingPenalty(int count, LineupContributionSector sector) => sector switch
    {
        LineupContributionSector.CentralDefence => count switch { 2 => .964, 3 => .90, _ => 1.0 },
        LineupContributionSector.InnerMidfield => count switch { 2 => .935, 3 => .825, _ => 1.0 },
        LineupContributionSector.Forward => count switch { 2 => .945, 3 => .865, _ => 1.0 },
        _ => 1.0
    };
}

public enum TeamAttitude { Normal, PIC, MOTS }
public enum TeamLocation { Home, Away, AwayDerby }

public sealed class TeamMatchContext
{
    public int TacticType { get; init; }
    public int TacticLevel { get; init; }
    public TeamAttitude Attitude { get; init; } = TeamAttitude.Normal;
    public TeamLocation Location { get; init; } = TeamLocation.Away;
    public bool IsHome { get; init; }
    public int CoachModifier { get; init; }
    public double TeamSpirit { get; init; }
    public double Confidence { get; init; }
    public MatchWeather Weather { get; init; } = MatchWeather.Normal;
    public int Minute { get; init; }
    public IReadOnlyDictionary<int, PlayerBehaviour> SlotBehaviours { get; init; } = new Dictionary<int, PlayerBehaviour>();
}
