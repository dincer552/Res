using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Hattrick-style seven-sector rating calculator.
/// Coefficients are based on the public/community contribution research.
/// The engine keeps the raw sector calculation separate from display conversion.
/// </summary>
public sealed class RegionalRatingEngine
{
    public RegionalRatingSnapshot Calculate(
        IReadOnlyList<RegionalPlayer> players,
        RatingContext? context = null)
    {
        context ??= RatingContext.Default;
        var s = Enum.GetValues<RatingSector>().ToDictionary(x => x, _ => 0d);

        foreach (var p in players)
        {
            var form = FormFactor(p.Form);
            var loyalty = p.Loyalty > 0 ? p.Loyalty / 19.0 : 0.0;
            var k = new EffectiveSkills(
                (p.Keeper + loyalty) * form,
                (p.Defending + loyalty) * form,
                (p.Playmaking + loyalty) * form,
                (p.Passing + loyalty) * form,
                (p.Winger + loyalty) * form,
                (p.Scoring + loyalty) * form);

            AddContribution(s, p, k);
        }

        // Central-position overcrowding reduces the contribution before XP is added.
        ApplyOvercrowding(s, players);
        // Experience is a flat contribution added after skill/form/overcrowding.
        ApplyExperience(s, players);
        ApplyContext(s, context);

        return Snapshot(s);
    }

    /// <summary>
    /// Explicit lineup entry point. The UI has 14 possible position boxes;
    /// empty boxes are ignored and the filled boxes are converted into the
    /// same seven-sector engine used for both teams.
    /// </summary>
    public RegionalRatingSnapshot CalculateLineup(
        Lineup lineup,
        IReadOnlyList<Player> players,
        RatingContext? context = null)
    {
        var byId = players.ToDictionary(p => p.Id);
        var regionalPlayers = lineup.Slots
            .Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId))
            .Select(s => ToRegionalPlayer(s, byId[s.PlayerId]))
            .ToList();
        return Calculate(regionalPlayers, context);
    }

    /// <summary>Calculates the fourteen displayed sectors: seven for each team.</summary>
    public RegionalRatingPair CalculatePair(
        Lineup ownLineup,
        IReadOnlyList<Player> ownPlayers,
        Lineup opponentLineup,
        IReadOnlyList<Player> opponentPlayers,
        RatingContext? ownContext = null,
        RatingContext? opponentContext = null)
        => new(
            CalculateLineup(ownLineup, ownPlayers, ownContext),
            CalculateLineup(opponentLineup, opponentPlayers, opponentContext));

    private static RegionalRatingSnapshot Snapshot(IDictionary<RatingSector, double> s)
        => new(
            s[RatingSector.LeftDefence], s[RatingSector.CentralDefence], s[RatingSector.RightDefence],
            s[RatingSector.Midfield],
            s[RatingSector.LeftAttack], s[RatingSector.CentralAttack], s[RatingSector.RightAttack],
            Display(s[RatingSector.LeftDefence]), Display(s[RatingSector.CentralDefence]), Display(s[RatingSector.RightDefence]),
            Display(s[RatingSector.Midfield]),
            Display(s[RatingSector.LeftAttack]), Display(s[RatingSector.CentralAttack]), Display(s[RatingSector.RightAttack]));

    private static void AddContribution(IDictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        switch (p.Position)
        {
            case RegionalPosition.Goalkeeper:
                Add(s, RatingSector.CentralDefence, k.Keeper * .165 + k.Defending * .079);
                // These coefficients are per side, not a total to be split in half.
                AddBothSides(s, RatingSector.LeftDefence, RatingSector.RightDefence,
                    k.Keeper * .183 + k.Defending * .082);
                break;

            case RegionalPosition.CentralDefender:
            {
                var central = p.Order switch
                {
                    PlayerOrder.Offensive => k.Defending * .130 + k.Playmaking * .047,
                    PlayerOrder.TowardsWing => k.Defending * .133 + k.Playmaking * .023,
                    _ => k.Defending * .186 + k.Playmaking * .035
                };
                var side = p.Order switch
                {
                    PlayerOrder.TowardsWing => k.Defending * .217,
                    PlayerOrder.Offensive => k.Defending * .058,
                    _ => k.Defending * .077
                };
                Add(s, RatingSector.CentralDefence, central);
                if (p.Side == PlayerSide.Center)
                    AddBothSides(s, RatingSector.LeftDefence, RatingSector.RightDefence, side);
                else
                    Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence, side);

                if (p.Order == PlayerOrder.TowardsWing)
                    Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, k.Passing * .063);
                break;
            }

            case RegionalPosition.WingBack:
            {
                var cd = p.Order switch { PlayerOrder.Defensive => .089, PlayerOrder.TowardsMiddle => .126, PlayerOrder.Offensive => .071, _ => .083 };
                var sd = p.Order switch { PlayerOrder.Defensive => .284, PlayerOrder.TowardsMiddle => .209, PlayerOrder.Offensive => .175, _ => .268 };
                var pm = p.Order switch { PlayerOrder.Defensive => .009, PlayerOrder.Offensive => .032, _ => .023 };
                var wing = p.Order switch { PlayerOrder.Defensive => .082, PlayerOrder.TowardsMiddle => .072, PlayerOrder.Offensive => .163, _ => .129 };
                var sideDef = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
                var sideAtt = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
                Add(s, RatingSector.CentralDefence, k.Defending * cd);
                Add(s, sideDef, k.Defending * sd);
                Add(s, RatingSector.Midfield, k.Playmaking * pm);
                Add(s, sideAtt, k.Winger * wing);
                break;
            }

            case RegionalPosition.InnerMidfielder:
            {
                var (cd, sd, pm, sidePass, centralPass, centralScoring, sideWinger) = p.Order switch
                {
                    PlayerOrder.Defensive => (.115, .040, .131, .018, .039, .028, 0d),
                    PlayerOrder.Offensive => (.115, .040, .131, .018, .039, .025, 0d),
                    PlayerOrder.TowardsWing => (.059, .068, .113, .064, .038, 0d, .117),
                    _ => (.070, .028, .139, .028, .057, .038, 0d)
                };
                Add(s, RatingSector.CentralDefence, k.Defending * cd);
                var sideDef = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
                if (p.Side == PlayerSide.Center)
                    AddBothSides(s, RatingSector.LeftDefence, RatingSector.RightDefence, k.Defending * sd);
                else
                    Add(s, sideDef, k.Defending * sd);

                Add(s, RatingSector.Midfield, k.Playmaking * pm);
                if (p.Side == PlayerSide.Center)
                    AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Passing * sidePass);
                else
                    Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, k.Passing * sidePass);
                Add(s, RatingSector.CentralAttack, k.Passing * centralPass + k.Scoring * centralScoring);
                if (sideWinger > 0)
                {
                    if (p.Side == PlayerSide.Center)
                        AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Winger * sideWinger);
                    else
                        Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, k.Winger * sideWinger);
                }
                break;
            }

            case RegionalPosition.Winger:
            {
                var sideDef = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
                var sideAtt = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
                var (cd, sd, pm, passSide, wingSide, passCenter) = p.Order switch
                {
                    PlayerOrder.Defensive => (.050, .148, .054, .185, .044, .009),
                    PlayerOrder.TowardsMiddle => (.047, .093, .082, .160, .043, .026),
                    PlayerOrder.Offensive => (.016, .055, .054, .247, .062, .024),
                    _ => (.037, .104, .065, .219, .054, .018)
                };
                Add(s, RatingSector.CentralDefence, k.Defending * cd);
                Add(s, sideDef, k.Defending * sd);
                Add(s, RatingSector.Midfield, k.Playmaking * pm);
                Add(s, sideAtt, k.Passing * passSide + k.Winger * wingSide);
                Add(s, RatingSector.CentralAttack, k.Passing * passCenter);
                break;
            }

            case RegionalPosition.Forward:
            {
                if (p.Side == PlayerSide.Center)
                {
                    switch (p.Order)
                    {
                        case PlayerOrder.TowardsWing:
                            Add(s, RatingSector.Midfield, k.Playmaking * .024);
                            AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .093 + k.Passing * .101 + k.Winger * .044);
                            Add(s, RatingSector.CentralAttack, k.Passing * .102 + k.Scoring * .044);
                            break;
                        case PlayerOrder.Defensive:
                            Add(s, RatingSector.Midfield, k.Playmaking * .058);
                            AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .030 + k.Passing * .033 + k.Winger * .059);
                            Add(s, RatingSector.CentralAttack, k.Passing * .108 + k.Scoring * .102);
                            break;
                        default:
                            Add(s, RatingSector.Midfield, k.Playmaking * .041);
                            AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .058 + k.Passing * .048 + k.Winger * .032);
                            Add(s, RatingSector.CentralAttack, k.Passing * .178 + k.Scoring * .066);
                            break;
                    }
                }
                else
                {
                    var side = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
                    var opposite = p.Side == PlayerSide.Left ? RatingSector.RightAttack : RatingSector.LeftAttack;
                    switch (p.Order)
                    {
                        case PlayerOrder.TowardsWing:
                            Add(s, RatingSector.Midfield, k.Playmaking * .024);
                            Add(s, side, k.Scoring * .093 + k.Passing * .101 + k.Winger * .044);
                            Add(s, opposite, k.Winger * .017);
                            Add(s, RatingSector.CentralAttack, k.Passing * .102 + k.Scoring * .044);
                            break;
                        case PlayerOrder.Defensive:
                            Add(s, RatingSector.Midfield, k.Playmaking * .058);
                            Add(s, side, k.Scoring * .030 + k.Passing * .033 + k.Winger * .059);
                            Add(s, RatingSector.CentralAttack, k.Passing * .108 + k.Scoring * .102);
                            break;
                        default:
                            Add(s, RatingSector.Midfield, k.Playmaking * .041);
                            Add(s, side, k.Scoring * .058 + k.Passing * .048 + k.Winger * .032);
                            Add(s, RatingSector.CentralAttack, k.Passing * .178 + k.Scoring * .066);
                            break;
                    }
                }
                break;
            }
        }
    }

    private static void ApplyExperience(IDictionary<RatingSector, double> s, IReadOnlyList<RegionalPlayer> players)
    {
        foreach (var p in players)
        {
            var bonus = ExperienceBonus(p.Experience);
            if (bonus <= 0) continue;

            // Schum/community research: experience is a flat contribution added
            // after skill/form/overcrowding. It depends on the rating line, not
            // on the player's raw skill coefficient for that line.
            switch (p.Position)
            {
                case RegionalPosition.Goalkeeper:
                    AddDefenceExperience(s, bonus, true, true, true);
                    break;

                case RegionalPosition.CentralDefender:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideDefenceExperience(s, p.Side, bonus * .345);
                    if (p.Order == PlayerOrder.TowardsWing)
                        Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, bonus * .375);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    break;

                case RegionalPosition.WingBack:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideDefenceExperience(s, p.Side, bonus * .345);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, bonus * .375);
                    break;

                case RegionalPosition.InnerMidfielder:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideDefenceExperience(s, p.Side, bonus * .345);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    AddSideAttackExperience(s, p.Side, bonus * .375);
                    break;

                case RegionalPosition.Winger:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideDefenceExperience(s, p.Side, bonus * .345);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, bonus * .375);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    break;

                case RegionalPosition.Forward:
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    AddSideAttackExperience(s, p.Side, bonus * .375);
                    break;
            }
        }
    }

    private static void AddDefenceExperience(IDictionary<RatingSector, double> s, double bonus, bool left, bool center, bool right)
    {
        if (left) Add(s, RatingSector.LeftDefence, bonus * .345);
        if (center) Add(s, RatingSector.CentralDefence, bonus * .480);
        if (right) Add(s, RatingSector.RightDefence, bonus * .345);
    }

    private static void AddSideDefenceExperience(IDictionary<RatingSector, double> s, PlayerSide side, double value)
    {
        if (side == PlayerSide.Center)
        {
            Add(s, RatingSector.LeftDefence, value);
            Add(s, RatingSector.RightDefence, value);
        }
        else
            Add(s, side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence, value);
    }

    private static void AddSideAttackExperience(IDictionary<RatingSector, double> s, PlayerSide side, double value)
    {
        if (side == PlayerSide.Center)
        {
            Add(s, RatingSector.LeftAttack, value);
            Add(s, RatingSector.RightAttack, value);
        }
        else
            Add(s, side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack, value);
    }

    private static double ExperienceBonus(double experience)
    {
        var values = new[] { 0.0, 0.0, .40, .64, .80, .93, 1.04, 1.13, 1.20, 1.27, 1.33, 1.39, 1.44, 1.49, 1.53, 1.57, 1.61, 1.64, 1.67, 1.71, 1.73 };
        var level = Math.Clamp((int)Math.Round(experience), 1, 20);
        return values[level];
    }

    private static void ApplyOvercrowding(IDictionary<RatingSector, double> s, IReadOnlyList<RegionalPlayer> players)
    {
        var centralDefenders = players.Count(p => p.Position == RegionalPosition.CentralDefender);
        if (centralDefenders == 2) s[RatingSector.CentralDefence] *= .964;
        else if (centralDefenders >= 3) s[RatingSector.CentralDefence] *= .900;

        var centralIm = players.Count(p => p.Position == RegionalPosition.InnerMidfielder && p.Side == PlayerSide.Center);
        if (centralIm == 2) s[RatingSector.Midfield] *= .935;
        else if (centralIm >= 3) s[RatingSector.Midfield] *= .825;
    }

    private static void ApplyContext(IDictionary<RatingSector, double> s, RatingContext c)
    {
        var midfield = c.MatchLocation switch
        {
            MatchLocation.Home => 1.1989,
            MatchLocation.DerbyAway => 1.10,
            _ => 1.0
        };
        midfield *= c.Attitude switch
        {
            TeamAttitude.MatchOfTheSeason => 83.0 / 75.0,
            TeamAttitude.PlayItCool => .84,
            _ => 1.0
        };
        midfield *= c.Tactic switch
        {
            TeamTactic.CounterAttack => .93,
            TeamTactic.LongShots => .96,
            _ => 1.0
        };
        s[RatingSector.Midfield] *= midfield;
    }

    private static void AddBothSides(IDictionary<RatingSector, double> s, RatingSector left, RatingSector right, double valuePerSide)
    {
        s[left] += valuePerSide;
        s[right] += valuePerSide;
    }

    private static void Add(IDictionary<RatingSector, double> s, RatingSector sector, double value)
        => s[sector] += value;

    private static double FormFactor(double form)
    {
        var points = new[]
        {
            (1.5,.282),(2.0,.379),(2.5,.462),(3.0,.534),(3.5,.598),(4.0,.655),(4.5,.707),
            (5.0,.755),(5.5,.800),(6.0,.844),(6.5,.885),(7.0,.925),(7.5,.964),(8.0,1.0)
        };
        if (form <= points[0].Item1) return points[0].Item2;
        if (form >= points[^1].Item1) return points[^1].Item2;
        for (var i = 1; i < points.Length; i++)
        {
            if (form <= points[i].Item1)
            {
                var (x0,y0)=points[i-1];
                var (x1,y1)=points[i];
                return y0 + (form-x0)*(y1-y0)/(x1-x0);
            }
        }
        return 1.0;
    }

    public static double Display(double raw)
        => raw <= 0 ? .75 : Math.Floor(raw * 4.0) / 4.0 + .75;

    private static RegionalPlayer ToRegionalPlayer(Slot slot, Player p)
    {
        var position = slot.Code switch
        {
            "GK" => RegionalPosition.Goalkeeper,
            "DEF-L" or "DEF-R" => RegionalPosition.WingBack,
            "DEF-CL" or "DEF-C" or "DEF-CR" => RegionalPosition.CentralDefender,
            "W-L" or "W-R" => RegionalPosition.Winger,
            "IM-L" or "IM-C" or "IM-R" => RegionalPosition.InnerMidfielder,
            "FW-L" or "FW-C" or "FW-R" => RegionalPosition.Forward,
            _ => RegionalPosition.InnerMidfielder
        };
        var side = slot.Code.EndsWith("-L", StringComparison.Ordinal) ? PlayerSide.Left
            : slot.Code.EndsWith("-R", StringComparison.Ordinal) ? PlayerSide.Right
            : PlayerSide.Center;
        return new RegionalPlayer(
            p.Id, position, side, slot.Order,
            p.Keeper, p.Defending, p.Playmaking, p.Passing, p.Winger, p.Scoring,
            p.Form, 0, p.Experience);
    }
}

public enum RatingSector { LeftDefence, CentralDefence, RightDefence, Midfield, LeftAttack, CentralAttack, RightAttack }
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

public sealed record RatingContext(MatchLocation MatchLocation, TeamAttitude Attitude, TeamTactic Tactic)
{
    public static RatingContext Default => new(MatchLocation.Away, TeamAttitude.Normal, TeamTactic.Normal);
}

public sealed record RegionalRatingPair(RegionalRatingSnapshot Own, RegionalRatingSnapshot Opponent)
{
    public IReadOnlyList<double> OwnSeven => new[] { Own.LeftDefence, Own.CentralDefence, Own.RightDefence, Own.Midfield, Own.LeftAttack, Own.CentralAttack, Own.RightAttack };
    public IReadOnlyList<double> OpponentSeven => new[] { Opponent.LeftDefence, Opponent.CentralDefence, Opponent.RightDefence, Opponent.Midfield, Opponent.LeftAttack, Opponent.CentralAttack, Opponent.RightAttack };
}

public sealed record RegionalRatingSnapshot(
    double RawLeftDefence, double RawCentralDefence, double RawRightDefence, double RawMidfield,
    double RawLeftAttack, double RawCentralAttack, double RawRightAttack,
    double LeftDefence, double CentralDefence, double RightDefence, double Midfield,
    double LeftAttack, double CentralAttack, double RightAttack)
{
    public double TotalDefence => LeftDefence + CentralDefence + RightDefence;
    public double TotalAttack => LeftAttack + CentralAttack + RightAttack;
}

internal readonly record struct EffectiveSkills(double Keeper, double Defending, double Playmaking, double Passing, double Winger, double Scoring);