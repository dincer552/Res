using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Regional team-rating engine using the researched Hattrick contribution
/// coefficients. The published coefficient table already contains a standard
/// form/stamina/experience uplift, so player form and experience are applied
/// relative to that baseline rather than stacked a second time.
/// </summary>
public sealed class RegionalRatingEngine
{
    private const double BaselineFormFactor = 0.755;      // solid/current form 5
    private const double BaselineExperienceBonus = 1.13; // solid experience

    public RegionalRatingSnapshot Calculate(IReadOnlyList<RegionalPlayer> players, RatingContext? context = null)
    {
        context ??= RatingContext.Default;
        var sectors = Empty();
        var centralDefenders = players.Count(p => p.Position == RegionalPosition.CentralDefender);
        var centralMidfielders = players.Count(p => p.Position == RegionalPosition.InnerMidfielder);

        foreach (var p in players)
        {
            // Contribution coefficients are already calibrated with a standard
            // form/experience amount. Apply only the delta from that baseline.
            var formMultiplier = FormFactor(p.Form) / BaselineFormFactor;
            var loyalty = LoyaltyEffect(p.Loyalty);
            var experienceDelta = ExperienceBonus(p.Experience) - BaselineExperienceBonus;
            var k = new EffectiveSkills(
                p.Keeper + loyalty + experienceDelta,
                p.Defending + loyalty + experienceDelta,
                p.Playmaking + loyalty + experienceDelta,
                p.Passing + loyalty + experienceDelta,
                p.Winger + loyalty + experienceDelta,
                p.Scoring + loyalty + experienceDelta,
                formMultiplier);
            AddPositionContribution(sectors, p, k, centralDefenders, centralMidfielders);
        }

        ApplyContext(sectors, context);
        return ToSnapshot(sectors);
    }

    public RegionalRatingSnapshot CalculateLineup(Lineup lineup, IReadOnlyList<Player> players, RatingContext? context = null)
    {
        var byId = players.ToDictionary(p => p.Id);
        var mapped = lineup.Slots.Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId))
            .Select(s => ToRegionalPlayer(s, byId[s.PlayerId])).ToList();
        return Calculate(mapped, context);
    }

    public RegionalRatingPair CalculatePair(Lineup ownLineup, IReadOnlyList<Player> ownPlayers, Lineup opponentLineup,
        IReadOnlyList<Player> opponentPlayers, RatingContext? ownContext = null, RatingContext? opponentContext = null)
        => new(CalculateLineup(ownLineup, ownPlayers, ownContext), CalculateLineup(opponentLineup, opponentPlayers, opponentContext));

    private static Dictionary<RatingSector, double> Empty() => Enum.GetValues<RatingSector>().ToDictionary(x => x, _ => 0d);

    private static RegionalRatingSnapshot ToSnapshot(Dictionary<RatingSector, double> s) => new(
        s[RatingSector.LeftDefence], s[RatingSector.CentralDefence], s[RatingSector.RightDefence], s[RatingSector.Midfield],
        s[RatingSector.LeftAttack], s[RatingSector.CentralAttack], s[RatingSector.RightAttack],
        Display(s[RatingSector.LeftDefence]), Display(s[RatingSector.CentralDefence]), Display(s[RatingSector.RightDefence]),
        Display(s[RatingSector.Midfield]), Display(s[RatingSector.LeftAttack]), Display(s[RatingSector.CentralAttack]), Display(s[RatingSector.RightAttack]));

    private static void AddPositionContribution(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k,
        int centralDefenders, int centralMidfielders)
    {
        switch (p.Position)
        {
            case RegionalPosition.Goalkeeper:
                Add(s, RatingSector.CentralDefence, k.Keeper * .165 + k.Defending * .079, k.FormMultiplier);
                AddBothSides(s, RatingSector.LeftDefence, RatingSector.RightDefence, k.Keeper * .183 + k.Defending * .082, k.FormMultiplier);
                break;
            case RegionalPosition.CentralDefender:
                AddCentralDefender(s, p, k, centralDefenders); break;
            case RegionalPosition.WingBack:
                AddWingBack(s, p, k); break;
            case RegionalPosition.InnerMidfielder:
                AddInnerMidfielder(s, p, k, centralMidfielders); break;
            case RegionalPosition.Winger:
                AddWinger(s, p, k); break;
            case RegionalPosition.Forward:
                AddForward(s, p, k); break;
        }
    }

    private static double CentralDefencePenalty(int count) => count == 2 ? .964 : count >= 3 ? .900 : 1.0;
    private static double MidfieldPenalty(int count) => count == 2 ? .935 : count >= 3 ? .825 : 1.0;

    private static void AddCentralDefender(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k, int count)
    {
        var central = p.Order switch { PlayerOrder.Offensive => k.Defending * .130, PlayerOrder.TowardsWing => k.Defending * .133, _ => k.Defending * .186 };
        var side = p.Order switch { PlayerOrder.TowardsWing => k.Defending * .217, PlayerOrder.Offensive => k.Defending * .058, _ => k.Defending * .077 };
        var midfield = p.Order switch { PlayerOrder.Offensive => k.Playmaking * .047, PlayerOrder.TowardsWing => k.Playmaking * .023, _ => k.Playmaking * .035 }
            * CentralDefencePenalty(count);
        Add(s, RatingSector.CentralDefence, central, k.FormMultiplier);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence, side, k.FormMultiplier);
        Add(s, RatingSector.Midfield, midfield, k.FormMultiplier);
        if (p.Order == PlayerOrder.TowardsWing && p.Side != PlayerSide.Center)
            AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack, k.Passing * .063, k.FormMultiplier);
    }

    private static void AddWingBack(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var centralDef = p.Order switch { PlayerOrder.Defensive => .089, PlayerOrder.TowardsMiddle => .126, PlayerOrder.Offensive => .071, _ => .083 };
        var sideDef = p.Order switch { PlayerOrder.Defensive => .284, PlayerOrder.TowardsMiddle => .209, PlayerOrder.Offensive => .175, _ => .268 };
        var midfield = p.Order switch { PlayerOrder.Defensive => .009, PlayerOrder.Offensive => .032, _ => .023 };
        var sideAttack = p.Order switch { PlayerOrder.Defensive => .082, PlayerOrder.TowardsMiddle => .072, PlayerOrder.Offensive => .163, _ => .129 };
        var def = p.Side == PlayerSide.Left ? RatingSector.LeftDefence : RatingSector.RightDefence;
        var att = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
        Add(s, RatingSector.CentralDefence, k.Defending * centralDef, k.FormMultiplier);
        Add(s, def, k.Defending * sideDef, k.FormMultiplier);
        Add(s, RatingSector.Midfield, k.Playmaking * midfield, k.FormMultiplier);
        Add(s, att, k.Winger * sideAttack, k.FormMultiplier);
    }

    private static void AddInnerMidfielder(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k, int count)
    {
        var v = p.Order switch
        {
            PlayerOrder.Defensive => new OrderMatrix(.115,.040,.131,.018,.039,.028,0d),
            PlayerOrder.Offensive => new OrderMatrix(.115,.040,.131,.018,.039,.025,0d),
            PlayerOrder.TowardsWing => new OrderMatrix(.059,.068,.113,.064,.038,0d,.117),
            _ => new OrderMatrix(.070,.028,.139,.028,.057,.038,0d)
        };
        Add(s, RatingSector.CentralDefence, k.Defending * v.CentralDefence, k.FormMultiplier);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence, k.Defending * v.SideDefence, k.FormMultiplier);
        Add(s, RatingSector.Midfield, k.Playmaking * v.Midfield * MidfieldPenalty(count), k.FormMultiplier);
        var sidePass = k.Passing * v.SidePassing;
        if (p.Side == PlayerSide.Center) AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, sidePass, k.FormMultiplier);
        else AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack, sidePass, k.FormMultiplier);
        Add(s, RatingSector.CentralAttack, k.Passing * v.CenterPassing + k.Scoring * v.CenterScoring, k.FormMultiplier);
        if (v.SideWinger > 0)
        {
            if (p.Side == PlayerSide.Center) AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Winger * v.SideWinger, k.FormMultiplier);
            else AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack, k.Winger * v.SideWinger, k.FormMultiplier);
        }
    }

    private static void AddWinger(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var v = p.Order switch
        {
            PlayerOrder.Defensive => new WingerMatrix(.050,.148,.054,.185,.044,.009),
            PlayerOrder.TowardsMiddle => new WingerMatrix(.047,.093,.082,.160,.043,.026),
            PlayerOrder.Offensive => new WingerMatrix(.016,.055,.054,.247,.062,.024),
            _ => new WingerMatrix(.037,.104,.065,.219,.054,.018)
        };
        Add(s, RatingSector.CentralDefence, k.Defending * v.CentralDefence, k.FormMultiplier);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence, k.Defending * v.SideDefence, k.FormMultiplier);
        Add(s, RatingSector.Midfield, k.Playmaking * v.Midfield, k.FormMultiplier);
        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack, k.Passing * v.SidePassing + k.Winger * v.SideWinger, k.FormMultiplier);
        Add(s, RatingSector.CentralAttack, k.Passing * v.CenterPassing, k.FormMultiplier);
    }

    private static void AddForward(Dictionary<RatingSector, double> s, RegionalPlayer p, EffectiveSkills k)
    {
        var side = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
        var opposite = p.Side == PlayerSide.Left ? RatingSector.RightAttack : RatingSector.LeftAttack;
        switch (p.Order)
        {
            case PlayerOrder.TowardsWing:
                Add(s, RatingSector.Midfield, k.Playmaking * .024, k.FormMultiplier);
                if (p.Side == PlayerSide.Center) AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .093 + k.Passing * .101 + k.Winger * .044, k.FormMultiplier);
                else { Add(s, side, k.Scoring * .093 + k.Passing * .101 + k.Winger * .044, k.FormMultiplier); Add(s, opposite, k.Scoring * .018 + k.Passing * .034, k.FormMultiplier); }
                Add(s, RatingSector.CentralAttack, k.Passing * .102 + k.Scoring * .044, k.FormMultiplier); break;
            case PlayerOrder.Defensive:
                Add(s, RatingSector.Midfield, k.Playmaking * .058, k.FormMultiplier);
                if (p.Side == PlayerSide.Center) AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .030 + k.Passing * .033 + k.Winger * .059, k.FormMultiplier);
                else { Add(s, side, k.Scoring * .030 + k.Passing * .033 + k.Winger * .059, k.FormMultiplier); Add(s, opposite, k.Scoring * .030 + k.Passing * .033, k.FormMultiplier); }
                Add(s, RatingSector.CentralAttack, k.Scoring * .102 + k.Passing * .108, k.FormMultiplier); break;
            default:
                Add(s, RatingSector.Midfield, k.Playmaking * .041, k.FormMultiplier);
                if (p.Side == PlayerSide.Center) AddBothSides(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Scoring * .058 + k.Passing * .048 + k.Winger * .032, k.FormMultiplier);
                else { var core = k.Scoring * .058 + k.Passing * .048; Add(s, side, core + k.Winger * .032, k.FormMultiplier); Add(s, opposite, core, k.FormMultiplier); }
                Add(s, RatingSector.CentralAttack, k.Scoring * .178 + k.Passing * .066, k.FormMultiplier); break;
        }
    }

    private static void ApplyContext(Dictionary<RatingSector, double> s, RatingContext c)
    {
        var midfield = c.MatchLocation switch { MatchLocation.Home => 1.19892, MatchLocation.DerbyAway => 1.11493, _ => 1.0 };
        midfield *= c.Attitude switch { TeamAttitude.MatchOfTheSeason => 1.1149, TeamAttitude.PlayItCool => .83945, _ => 1.0 };
        if (c.Tactic == TeamTactic.CounterAttack) midfield *= .93;
        switch (c.Tactic)
        {
            case TeamTactic.AttackMiddle: s[RatingSector.LeftDefence] *= .85; s[RatingSector.RightDefence] *= .85; break;
            case TeamTactic.AttackWings: s[RatingSector.CentralDefence] *= .85; break;
            case TeamTactic.Creative: s[RatingSector.LeftDefence] *= .93; s[RatingSector.CentralDefence] *= .93; s[RatingSector.RightDefence] *= .93; break;
            case TeamTactic.LongShots: s[RatingSector.LeftAttack] *= .96; s[RatingSector.CentralAttack] *= .96; s[RatingSector.RightAttack] *= .96; break;
        }
        if (c.MatchMinute > 0) ApplyStaminaDecay(s, c);
        s[RatingSector.Midfield] *= midfield;
        if (c.GoalDifference >= 2 && !c.IgnoreLeadRetreat)
        {
            var steps = Math.Min(c.GoalDifference - 1, 7);
            var protection = 1.0 + steps * .075;
            var attack = 1.0 - steps * .09;
            s[RatingSector.LeftDefence] *= protection; s[RatingSector.CentralDefence] *= protection; s[RatingSector.RightDefence] *= protection;
            s[RatingSector.LeftAttack] *= attack; s[RatingSector.CentralAttack] *= attack; s[RatingSector.RightAttack] *= attack;
        }
    }

    private static void ApplyStaminaDecay(Dictionary<RatingSector, double> s, RatingContext c)
    {
        var minute = Math.Clamp(c.MatchMinute, 0, 120);
        s[RatingSector.Midfield] *= 1.0 - .10 * Math.Clamp(minute / 90.0, 0, 1);
    }

    private static double LoyaltyEffect(double loyalty) => loyalty <= 0 ? 0 : Math.Clamp(loyalty * .05, 0.0, 1.0);

    private static double ExperienceBonus(double experience)
    {
        var values = new[] { 0.00,0.00,.40,.64,.80,.93,1.04,1.13,1.20,1.27,1.33,1.39,1.44,1.49,1.53,1.57,1.61,1.64,1.67,1.71,1.73 };
        return values[Math.Clamp((int)Math.Round(experience), 1, 20)];
    }

    private static void AddBothSides(Dictionary<RatingSector, double> s, RatingSector left, RatingSector right, double value, double formMultiplier = 1.0)
    { s[left] += value * formMultiplier; s[right] += value * formMultiplier; }

    private static void AddSideOnly(Dictionary<RatingSector, double> s, PlayerSide side, RatingSector left, RatingSector right, double value, double formMultiplier = 1.0)
    { value *= formMultiplier; if (side == PlayerSide.Left) s[left] += value; else if (side == PlayerSide.Right) s[right] += value; else AddBothSides(s, left, right, value); }

    private static void Add(Dictionary<RatingSector, double> s, RatingSector sector, double value, double formMultiplier = 1.0) => s[sector] += value * formMultiplier;

    private static double FormFactor(double form)
    {
        var points = new[] { (1.5,.282),(2.0,.379),(2.5,.462),(3.0,.534),(3.5,.598),(4.0,.655),(4.5,.707),(5.0,.755),(5.5,.800),(6.0,.844),(6.5,.885),(7.0,.925),(7.5,.964),(8.0,1.000) };
        if (form <= points[0].Item1) return points[0].Item2;
        if (form >= points[^1].Item1) return points[^1].Item2;
        for (var i=1;i<points.Length;i++) if (form <= points[i].Item1) { var (x0,y0)=points[i-1]; var (x1,y1)=points[i]; return y0+(form-x0)*(y1-y0)/(x1-x0); }
        return 1.0;
    }

    public static double Display(double raw) => raw <= 0 ? .75 : Math.Floor(raw * 4.0) / 4.0 + 1.0;

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
        var side = slot.Code.EndsWith("-L", StringComparison.Ordinal) ? PlayerSide.Left : slot.Code.EndsWith("-R", StringComparison.Ordinal) ? PlayerSide.Right : PlayerSide.Center;
        return new RegionalPlayer(p.Id, position, side, slot.Order, p.Keeper, p.Defending, p.Playmaking, p.Passing, p.Winger, p.Scoring, p.Form, p.Loyalty, p.Experience, p.Stamina);
    }

    private readonly record struct OrderMatrix(double CentralDefence,double SideDefence,double Midfield,double SidePassing,double CenterPassing,double CenterScoring,double SideWinger);
    private readonly record struct WingerMatrix(double CentralDefence,double SideDefence,double Midfield,double SidePassing,double SideWinger,double CenterPassing);
}

public enum RatingSector { LeftDefence, CentralDefence, RightDefence, Midfield, LeftAttack, CentralAttack, RightAttack }
public enum RegionalPosition { Goalkeeper, CentralDefender, WingBack, InnerMidfielder, Winger, Forward }
public enum PlayerOrder { Normal, Defensive, Offensive, TowardsWing, TowardsMiddle }
public enum PlayerSide { Left, Center, Right }
public enum MatchLocation { Away, Home, DerbyAway }
public enum TeamAttitude { Normal, MatchOfTheSeason, PlayItCool }
public enum TeamTactic { Normal, CounterAttack, LongShots, AttackMiddle, AttackWings, Creative }
public sealed record RegionalPlayer(int Id, RegionalPosition Position, PlayerSide Side, PlayerOrder Order, double Keeper, double Defending, double Playmaking, double Passing, double Winger, double Scoring, double Form, double Loyalty, double Experience, double Stamina);
public sealed record RatingContext(MatchLocation MatchLocation, TeamAttitude Attitude, TeamTactic Tactic)
{ public int MatchMinute { get; init; } public int GoalDifference { get; init; } public bool IgnoreLeadRetreat { get; init; } public static RatingContext Default => new(MatchLocation.Away, TeamAttitude.Normal, TeamTactic.Normal); }
public sealed record RegionalRatingPair(RegionalRatingSnapshot Own, RegionalRatingSnapshot Opponent)
{ public IReadOnlyList<double> OwnSeven=>new[]{Own.LeftDefence,Own.CentralDefence,Own.RightDefence,Own.Midfield,Own.LeftAttack,Own.CentralAttack,Own.RightAttack}; public IReadOnlyList<double> OpponentSeven=>new[]{Opponent.LeftDefence,Opponent.CentralDefence,Opponent.RightDefence,Opponent.Midfield,Opponent.LeftAttack,Opponent.CentralAttack,Opponent.RightAttack}; }
public sealed record RegionalRatingSnapshot(double RawLeftDefence,double RawCentralDefence,double RawRightDefence,double RawMidfield,double RawLeftAttack,double RawCentralAttack,double RawRightAttack,double LeftDefence,double CentralDefence,double RightDefence,double Midfield,double LeftAttack,double CentralAttack,double RightAttack)
{ public double TotalDefence=>LeftDefence+CentralDefence+RightDefence; public double TotalAttack=>LeftAttack+CentralAttack+RightAttack; }
internal readonly record struct EffectiveSkills(double Keeper,double Defending,double Playmaking,double Passing,double Winger,double Scoring,double FormMultiplier);