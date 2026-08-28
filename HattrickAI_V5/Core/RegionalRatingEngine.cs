using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Hattrick-style seven-sector rating engine.
///
/// Phase 1 keeps the researched position/order contribution coefficients
/// isolated from UI and lineup optimization. Hattrick does not publish the
/// complete match-engine formula, so the coefficient table is treated as a
/// calibration layer and validated against real match reports.
/// </summary>
public sealed class RegionalRatingEngine
{
    public RegionalRatingSnapshot Calculate(
        IReadOnlyList<RegionalPlayer> players,
        RatingContext? context = null)
    {
        context ??= RatingContext.Default;
        var sectors = CreateSectors();

        foreach (var p in players)
        {
            var formFactor = FormFactor(p.Form);
            var loyaltyBonus = p.Loyalty / 19.0;
            var skills = new EffectiveSkills(
                (p.Keeper + loyaltyBonus) * formFactor,
                (p.Defending + loyaltyBonus) * formFactor,
                (p.Playmaking + loyaltyBonus) * formFactor,
                (p.Passing + loyaltyBonus) * formFactor,
                (p.Winger + loyaltyBonus) * formFactor,
                (p.Scoring + loyaltyBonus) * formFactor);

            AddContribution(sectors, p, skills);
        }

        ApplyOvercrowding(sectors, players);
        ApplyMatchContext(sectors, context);

        return Snapshot(sectors);
    }

    private static Dictionary<RatingSector, double> CreateSectors() => new()
    {
        [RatingSector.LeftDefence] = 0,
        [RatingSector.CentralDefence] = 0,
        [RatingSector.RightDefence] = 0,
        [RatingSector.Midfield] = 0,
        [RatingSector.LeftAttack] = 0,
        [RatingSector.CentralAttack] = 0,
        [RatingSector.RightAttack] = 0
    };

    private static RegionalRatingSnapshot Snapshot(IReadOnlyDictionary<RatingSector, double> s)
        => new(
            s[RatingSector.LeftDefence],
            s[RatingSector.CentralDefence],
            s[RatingSector.RightDefence],
            s[RatingSector.Midfield],
            s[RatingSector.LeftAttack],
            s[RatingSector.CentralAttack],
            s[RatingSector.RightAttack],
            Display(s[RatingSector.LeftDefence]),
            Display(s[RatingSector.CentralDefence]),
            Display(s[RatingSector.RightDefence]),
            Display(s[RatingSector.Midfield]),
            Display(s[RatingSector.LeftAttack]),
            Display(s[RatingSector.CentralAttack]),
            Display(s[RatingSector.RightAttack]));

    private static void AddContribution(
        IDictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        switch (p.Position)
        {
            case RegionalPosition.Goalkeeper:
                Add(s, RatingSector.CentralDefence, k.Keeper * 0.165 + k.Defending * 0.079);
                // A goalkeeper is central; his side-defence contribution is split
                // equally between left and right, never sent entirely to the right.
                AddCentralOrSide(s, RatingSector.LeftDefence, RatingSector.RightDefence,
                    k.Keeper * 0.183 + k.Defending * 0.082, PlayerSide.Center);
                break;

            case RegionalPosition.CentralDefender:
                AddCentralDefender(s, p, k);
                break;

            case RegionalPosition.WingBack:
                AddWingBack(s, p, k);
                break;

            case RegionalPosition.InnerMidfielder:
                AddInnerMidfielder(s, p, k);
                break;

            case RegionalPosition.Winger:
                AddWinger(s, p, k);
                break;

            case RegionalPosition.Forward:
                AddForward(s, p, k);
                break;
        }
    }

    private static void AddCentralDefender(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var centralDef = p.Order switch
        {
            PlayerOrder.Offensive => k.Defending * 0.130 + k.Playmaking * 0.047,
            PlayerOrder.TowardsWing => k.Defending * 0.133 + k.Playmaking * 0.023,
            _ => k.Defending * 0.186 + k.Playmaking * 0.035
        };

        var sideDef = p.Order switch
        {
            PlayerOrder.TowardsWing => k.Defending * 0.217,
            PlayerOrder.Offensive => k.Defending * 0.058,
            _ => k.Defending * 0.077
        };

        Add(s, RatingSector.CentralDefence, centralDef);
        AddCentralOrSide(s, RatingSector.LeftDefence, RatingSector.RightDefence, sideDef, p.Side);

        if (p.Order == PlayerOrder.TowardsWing)
            AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                k.Passing * 0.063, p.Side);
    }

    private static void AddWingBack(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var side = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
        var attack = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;

        var cd = p.Order switch
        {
            PlayerOrder.Defensive => k.Defending * 0.089,
            PlayerOrder.TowardsMiddle => k.Defending * 0.126,
            PlayerOrder.Offensive => k.Defending * 0.071,
            _ => k.Defending * 0.083
        };
        var sd = p.Order switch
        {
            PlayerOrder.Defensive => k.Defending * 0.284,
            PlayerOrder.TowardsMiddle => k.Defending * 0.209,
            PlayerOrder.Offensive => k.Defending * 0.175,
            _ => k.Defending * 0.268
        };
        var pm = p.Order switch
        {
            PlayerOrder.Defensive => k.Playmaking * 0.009,
            PlayerOrder.Offensive => k.Playmaking * 0.032,
            _ => k.Playmaking * 0.023
        };
        var wing = p.Order switch
        {
            PlayerOrder.Defensive => k.Winger * 0.082,
            PlayerOrder.TowardsMiddle => k.Winger * 0.072,
            PlayerOrder.Offensive => k.Winger * 0.163,
            _ => k.Winger * 0.129
        };

        Add(s, RatingSector.CentralDefence, cd);
        Add(s, side, sd);
        Add(s, RatingSector.Midfield, pm);
        Add(s, attack, wing);
    }

    private static void AddInnerMidfielder(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var (cd, sd, pm, sidePass, centralPass, centralScore) = p.Order switch
        {
            PlayerOrder.Defensive => (0.115, 0.040, 0.131, 0.018, 0.039, 0.028),
            PlayerOrder.Offensive => (0.115, 0.040, 0.131, 0.018, 0.039, 0.025),
            PlayerOrder.TowardsWing => (0.059, 0.068, 0.113, 0.064, 0.038, 0.000),
            _ => (0.070, 0.028, 0.139, 0.028, 0.057, 0.038)
        };

        Add(s, RatingSector.CentralDefence, k.Defending * cd);
        AddCentralOrSide(s, RatingSector.LeftDefence, RatingSector.RightDefence,
            k.Defending * sd, p.Side);
        Add(s, RatingSector.Midfield, k.Playmaking * pm);

        AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
            k.Passing * sidePass, p.Side);
        Add(s, RatingSector.CentralAttack, k.Passing * centralPass + k.Scoring * centralScore);

        if (p.Order == PlayerOrder.TowardsWing)
            AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                k.Winger * 0.117, p.Side);
    }

    private static void AddWinger(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var sideDef = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
        var sideAtk = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
        var (cd, sd, pm, passSide, wingSide, passCenter) = p.Order switch
        {
            PlayerOrder.Defensive => (0.050, 0.148, 0.054, 0.185, 0.044, 0.009),
            PlayerOrder.TowardsMiddle => (0.047, 0.093, 0.082, 0.160, 0.043, 0.026),
            PlayerOrder.Offensive => (0.016, 0.055, 0.054, 0.247, 0.062, 0.024),
            _ => (0.037, 0.104, 0.065, 0.219, 0.054, 0.018)
        };

        Add(s, RatingSector.CentralDefence, k.Defending * cd);
        Add(s, sideDef, k.Defending * sd);
        Add(s, RatingSector.Midfield, k.Playmaking * pm);
        Add(s, sideAtk, k.Passing * passSide + k.Winger * wingSide);
        Add(s, RatingSector.CentralAttack, k.Passing * passCenter);
    }

    private static void AddForward(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        switch (p.Order)
        {
            case PlayerOrder.TowardsWing:
                Add(s, RatingSector.Midfield, k.Playmaking * 0.024);
                AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Scoring * 0.093 + k.Passing * 0.101 + k.Winger * 0.044, p.Side);
                AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Winger * 0.017, OppositeSide(p.Side));
                Add(s, RatingSector.CentralAttack, k.Passing * 0.102 + k.Scoring * 0.044);
                break;

            case PlayerOrder.Defensive:
                Add(s, RatingSector.Midfield, k.Playmaking * 0.058);
                AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Scoring * 0.030 + k.Passing * 0.033 + k.Winger * 0.059, p.Side);
                Add(s, RatingSector.CentralAttack, k.Passing * 0.108 + k.Scoring * 0.102);
                break;

            default:
                Add(s, RatingSector.Midfield, k.Playmaking * 0.041);
                AddCentralOrSide(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Scoring * 0.058 + k.Passing * 0.048 + k.Winger * 0.032, p.Side);
                Add(s, RatingSector.CentralAttack, k.Passing * 0.178 + k.Scoring * 0.066);
                break;
        }
    }

    private static void AddCentralOrSide(
        IDictionary<RatingSector, double> s,
        RatingSector left,
        RatingSector right,
        double value,
        PlayerSide side)
    {
        if (side == PlayerSide.Left)
            s[left] += value;
        else if (side == PlayerSide.Right)
            s[right] += value;
        else
        {
            s[left] += value / 2.0;
            s[right] += value / 2.0;
        }
    }

    private static PlayerSide OppositeSide(PlayerSide side) => side switch
    {
        PlayerSide.Left => PlayerSide.Right,
        PlayerSide.Right => PlayerSide.Left,
        _ => PlayerSide.Center
    };

    private static void ApplyOvercrowding(IDictionary<RatingSector, double> s, IReadOnlyList<RegionalPlayer> players)
    {
        // Phase 1 keeps the existing researched approximation isolated here.
        // Exact central-position loss is a later calibration task.
        var centralDefenders = players.Count(p => p.Position == RegionalPosition.CentralDefender);
        if (centralDefenders == 2) s[RatingSector.CentralDefence] *= 0.964;
        if (centralDefenders >= 3) s[RatingSector.CentralDefence] *= 0.900;

        var centralIm = players.Count(p => p.Position == RegionalPosition.InnerMidfielder && p.Side == PlayerSide.Center);
        if (centralIm == 2) s[RatingSector.Midfield] *= 0.935;
        if (centralIm >= 3) s[RatingSector.Midfield] *= 0.825;
    }

    private static void ApplyMatchContext(IDictionary<RatingSector, double> s, RatingContext c)
    {
        // Context is intentionally isolated from the Phase 1 contribution core.
        if (c.MatchLocation == MatchLocation.Home)
            s[RatingSector.Midfield] *= 1.1989;
        else if (c.MatchLocation == MatchLocation.DerbyAway)
            s[RatingSector.Midfield] *= 1.1149;

        if (c.Attitude == TeamAttitude.MatchOfTheSeason)
            s[RatingSector.Midfield] *= 1.10;
        else if (c.Attitude == TeamAttitude.PlayItCool)
            s[RatingSector.Midfield] *= 0.84;

        if (c.Tactic == TeamTactic.CounterAttack)
            s[RatingSector.Midfield] *= 0.93;
    }

    private static double FormFactor(double form)
    {
        var points = new[]
        {
            (1.5, .282), (2.0, .379), (2.5, .462), (3.0, .534),
            (3.5, .598), (4.0, .655), (4.5, .707), (5.0, .755),
            (5.5, .800), (6.0, .844), (6.5, .885), (7.0, .925),
            (7.5, .964), (8.0, 1.000)
        };
        if (form <= points[0].Item1) return points[0].Item2;
        if (form >= points[^1].Item1) return points[^1].Item2;

        for (var i = 1; i < points.Length; i++)
        {
            if (form <= points[i].Item1)
            {
                var (x0, y0) = points[i - 1];
                var (x1, y1) = points[i];
                return y0 + (form - x0) * (y1 - y0) / (x1 - x0);
            }
        }

        return 1.0;
    }

    /// <summary>
    /// Converts the underlying rating value to Hattrick's quarter-step display.
    /// 0.00 is displayed as 0.75; subsequent quarter buckets advance by 0.25.
    /// </summary>
    public static double Display(double raw)
    {
        if (raw <= 0) return 0.75;
        return Math.Floor(raw * 4.0) / 4.0 + 0.75;
    }
}

public enum RatingSector
{
    LeftDefence,
    CentralDefence,
    RightDefence,
    Midfield,
    LeftAttack,
    CentralAttack,
    RightAttack
}

public enum RegionalPosition { Goalkeeper, CentralDefender, WingBack, InnerMidfielder, Winger, Forward }
public enum PlayerOrder { Normal, Defensive, Offensive, TowardsWing, TowardsMiddle }
public enum PlayerSide { Left, Center, Right }
public enum MatchLocation { Away, Home, DerbyAway }
public enum TeamAttitude { Normal, MatchOfTheSeason, PlayItCool }
public enum TeamTactic { Normal, CounterAttack, LongShots }

public sealed record RegionalPlayer(
    int Id,
    RegionalPosition Position,
    PlayerSide Side,
    PlayerOrder Order,
    double Keeper,
    double Defending,
    double Playmaking,
    double Passing,
    double Winger,
    double Scoring,
    double Form,
    double Loyalty,
    double Experience);

public sealed record RatingContext(
    MatchLocation MatchLocation,
    TeamAttitude Attitude,
    TeamTactic Tactic)
{
    public static RatingContext Default => new(MatchLocation.Away, TeamAttitude.Normal, TeamTactic.Normal);
}

public sealed record RegionalRatingSnapshot(
    double RawLeftDefence,
    double RawCentralDefence,
    double RawRightDefence,
    double RawMidfield,
    double RawLeftAttack,
    double RawCentralAttack,
    double RawRightAttack,
    double LeftDefence,
    double CentralDefence,
    double RightDefence,
    double Midfield,
    double LeftAttack,
    double CentralAttack,
    double RightAttack)
{
    public double TotalDefence => LeftDefence + CentralDefence + RightDefence;
    public double TotalAttack => LeftAttack + CentralAttack + RightAttack;
}

internal readonly record struct EffectiveSkills(
    double Keeper,
    double Defending,
    double Playmaking,
    double Passing,
    double Winger,
    double Scoring);