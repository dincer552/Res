using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Hattrick regional-rating calculator for V5.
///
/// The public Hattrick contribution table is used as the position/order matrix.
/// The Schum model is used for the calculation order:
///   (skill + loyalty) * form * contribution * overcrowding
///   + experience contribution
/// followed by match-context modifiers and the quarter-step display conversion.
///
/// Important: Hattrick does not publish the complete live match-engine formula.
/// This class therefore keeps the researched pieces explicit and testable rather
/// than hiding them behind an unexplained global multiplier.
/// </summary>
public sealed class RegionalRatingEngine
{
    public RegionalRatingSnapshot Calculate(
        IReadOnlyList<RegionalPlayer> players,
        RatingContext? context = null)
    {
        context ??= RatingContext.Default;
        var sectors = Empty();

        // 1) Base contribution: skill + loyalty, then form, then the exact
        // position/order coefficient. This is the order described by Schum.
        foreach (var player in players)
        {
            var form = FormFactor(player.Form);
            var loyalty = LoyaltyEffect(player.Loyalty);
            var effective = new EffectiveSkills(
                (player.Keeper + loyalty) * form,
                (player.Defending + loyalty) * form,
                (player.Playmaking + loyalty) * form,
                (player.Passing + loyalty) * form,
                (player.Winger + loyalty) * form,
                (player.Scoring + loyalty) * form);

            AddPositionContribution(sectors, player, effective);
        }

        // 2) Central-line overcrowding happens after skill/form/order
        // contributions and before the separate experience contribution.
        ApplyOvercrowding(sectors, players);

        // 3) Experience is a flat skill-equivalent bonus. It is added only to
        // sectors that the player's actual position/order can influence.
        ApplyExperience(sectors, players);

        // 4) Context modifiers (venue, attitude, tactic, late-match retreat and
        // optional fatigue) are kept outside player contribution calculations.
        ApplyContext(sectors, context);

        return ToSnapshot(sectors);
    }

    /// <summary>
    /// The UI has 14 possible football slots. Empty slots are intentionally kept
    /// in the lineup but contribute zero; an occupied slot is converted into a
    /// RegionalPlayer and therefore participates in the same engine as every
    /// other position.
    /// </summary>
    public RegionalRatingSnapshot CalculateLineup(
        Lineup lineup,
        IReadOnlyList<Player> players,
        RatingContext? context = null)
    {
        var byId = players.ToDictionary(p => p.Id);
        var mapped = lineup.Slots
            .Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId))
            .Select(s => ToRegionalPlayer(s, byId[s.PlayerId]))
            .ToList();

        return Calculate(mapped, context);
    }

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

    private static Dictionary<RatingSector, double> Empty()
        => Enum.GetValues<RatingSector>().ToDictionary(x => x, _ => 0d);

    private static RegionalRatingSnapshot ToSnapshot(Dictionary<RatingSector, double> s)
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

    private static void AddPositionContribution(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        switch (p.Position)
        {
            case RegionalPosition.Goalkeeper:
                Add(s, RatingSector.CentralDefence, k.Keeper * .165 + k.Defending * .079);
                AddBoth(s, RatingSector.LeftDefence, RatingSector.RightDefence,
                    k.Keeper * .183 + k.Defending * .082);
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

    private static void AddCentralDefender(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        // Public matrix: normal/defensive/offensive/towards-wing CD variants.
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
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence, side);

        // A central defender moved towards the wing also creates side attack
        // through passing. The opposite wing is not touched.
        if (p.Order == PlayerOrder.TowardsWing && p.Side != PlayerSide.Center)
            AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                k.Passing * .063);
    }

    private static void AddWingBack(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        var centralDef = p.Order switch
        {
            PlayerOrder.Defensive => .089,
            PlayerOrder.TowardsMiddle => .126,
            PlayerOrder.Offensive => .071,
            _ => .083
        };
        var sideDef = p.Order switch
        {
            PlayerOrder.Defensive => .284,
            PlayerOrder.TowardsMiddle => .209,
            PlayerOrder.Offensive => .175,
            _ => .268
        };
        var midfield = p.Order switch
        {
            PlayerOrder.Defensive => .009,
            PlayerOrder.Offensive => .032,
            _ => .023
        };
        var sideAttack = p.Order switch
        {
            PlayerOrder.Defensive => .082,
            PlayerOrder.TowardsMiddle => .072,
            PlayerOrder.Offensive => .163,
            _ => .129
        };

        Add(s, RatingSector.CentralDefence, k.Defending * centralDef);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
            k.Defending * sideDef);
        Add(s, RatingSector.Midfield, k.Playmaking * midfield);
        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
            k.Winger * sideAttack);
    }

    private static void AddInnerMidfielder(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        var values = p.Order switch
        {
            PlayerOrder.Defensive => new OrderMatrix(.115, .040, .131, .018, .039, .028, 0d),
            PlayerOrder.Offensive => new OrderMatrix(.115, .040, .131, .018, .039, .025, 0d),
            PlayerOrder.TowardsWing => new OrderMatrix(.059, .068, .113, .064, .038, 0d, .117),
            _ => new OrderMatrix(.070, .028, .139, .028, .057, .038, 0d)
        };

        Add(s, RatingSector.CentralDefence, k.Defending * values.CentralDefence);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
            k.Defending * values.SideDefence);
        Add(s, RatingSector.Midfield, k.Playmaking * values.Midfield);

        if (p.Side == PlayerSide.Center)
            AddBoth(s, RatingSector.LeftAttack, RatingSector.RightAttack, k.Passing * values.SidePassing);
        else
            AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                k.Passing * values.SidePassing);

        Add(s, RatingSector.CentralAttack,
            k.Passing * values.CenterPassing + k.Scoring * values.CenterScoring);

        if (values.SideWinger > 0)
        {
            if (p.Side == PlayerSide.Center)
                AddBoth(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Winger * values.SideWinger);
            else
                AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                    k.Winger * values.SideWinger);
        }
    }

    private static void AddWinger(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        var values = p.Order switch
        {
            PlayerOrder.Defensive => new WingerMatrix(.050, .148, .054, .185, .044, .009),
            PlayerOrder.TowardsMiddle => new WingerMatrix(.047, .093, .082, .160, .043, .026),
            PlayerOrder.Offensive => new WingerMatrix(.016, .055, .054, .247, .062, .024),
            _ => new WingerMatrix(.037, .104, .065, .219, .054, .018)
        };

        Add(s, RatingSector.CentralDefence, k.Defending * values.CentralDefence);
        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
            k.Defending * values.SideDefence);
        Add(s, RatingSector.Midfield, k.Playmaking * values.Midfield);
        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
            k.Passing * values.SidePassing + k.Winger * values.SideWinger);
        Add(s, RatingSector.CentralAttack, k.Passing * values.CenterPassing);
    }

    private static void AddForward(
        Dictionary<RatingSector, double> s,
        RegionalPlayer p,
        EffectiveSkills k)
    {
        var side = p.Side == PlayerSide.Left ? RatingSector.LeftAttack : RatingSector.RightAttack;
        var opposite = p.Side == PlayerSide.Left ? RatingSector.RightAttack : RatingSector.LeftAttack;

        switch (p.Order)
        {
            case PlayerOrder.TowardsWing:
                Add(s, RatingSector.Midfield, k.Playmaking * .024);
                if (p.Side == PlayerSide.Center)
                {
                    AddBoth(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                        k.Scoring * .093 + k.Passing * .101 + k.Winger * .044);
                }
                else
                {
                    Add(s, side, k.Scoring * .093 + k.Passing * .101 + k.Winger * .044);
                    Add(s, opposite, k.Winger * .017);
                }
                Add(s, RatingSector.CentralAttack,
                    k.Passing * .102 + k.Scoring * .044);
                break;

            case PlayerOrder.Defensive:
                Add(s, RatingSector.Midfield, k.Playmaking * .058);
                if (p.Side == PlayerSide.Center)
                {
                    AddBoth(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                        k.Scoring * .030 + k.Passing * .033 + k.Winger * .059);
                }
                else
                {
                    Add(s, side, k.Scoring * .030 + k.Passing * .033 + k.Winger * .059);
                }
                Add(s, RatingSector.CentralAttack,
                    k.Passing * .108 + k.Scoring * .102);
                break;

            default:
                Add(s, RatingSector.Midfield, k.Playmaking * .041);
                if (p.Side == PlayerSide.Center)
                {
                    AddBoth(s, RatingSector.LeftAttack, RatingSector.RightAttack,
                        k.Scoring * .058 + k.Passing * .048 + k.Winger * .032);
                }
                else
                {
                    Add(s, side, k.Scoring * .058 + k.Passing * .048 + k.Winger * .032);
                }
                Add(s, RatingSector.CentralAttack,
                    k.Passing * .178 + k.Scoring * .066);
                break;
        }
    }

    private static void ApplyOvercrowding(
        Dictionary<RatingSector, double> s,
        IReadOnlyList<RegionalPlayer> players)
    {
        var centralDefenders = players.Count(p => p.Position == RegionalPosition.CentralDefender);
        if (centralDefenders == 2)
            s[RatingSector.CentralDefence] *= .964;
        else if (centralDefenders >= 3)
            s[RatingSector.CentralDefence] *= .900;

        var centralIm = players.Count(p =>
            p.Position == RegionalPosition.InnerMidfielder && p.Side == PlayerSide.Center);
        if (centralIm == 2)
            s[RatingSector.Midfield] *= .935;
        else if (centralIm >= 3)
            s[RatingSector.Midfield] *= .825;

        var centralForwards = players.Count(p =>
            p.Position == RegionalPosition.Forward && p.Side == PlayerSide.Center);
        if (centralForwards == 2)
            s[RatingSector.CentralAttack] *= .945;
        else if (centralForwards >= 3)
            s[RatingSector.CentralAttack] *= .865;
    }

    private static void ApplyExperience(
        Dictionary<RatingSector, double> s,
        IReadOnlyList<RegionalPlayer> players)
    {
        foreach (var p in players)
        {
            var bonus = ExperienceBonus(p.Experience);
            if (bonus <= 0) continue;

            switch (p.Position)
            {
                case RegionalPosition.Goalkeeper:
                    Add(s, RatingSector.LeftDefence, bonus * .345);
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    Add(s, RatingSector.RightDefence, bonus * .345);
                    break;

                case RegionalPosition.CentralDefender:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
                        bonus * .345);
                    if (p.Order == PlayerOrder.TowardsWing && p.Side != PlayerSide.Center)
                        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                            bonus * .375);
                    break;

                case RegionalPosition.WingBack:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
                        bonus * .345);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                        bonus * .375);
                    break;

                case RegionalPosition.InnerMidfielder:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    if (p.Side == PlayerSide.Center)
                    {
                        Add(s, RatingSector.LeftDefence, bonus * .345);
                        Add(s, RatingSector.RightDefence, bonus * .345);
                        Add(s, RatingSector.LeftAttack, bonus * .375);
                        Add(s, RatingSector.RightAttack, bonus * .375);
                    }
                    else
                    {
                        AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
                            bonus * .345);
                        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                            bonus * .375);
                    }
                    break;

                case RegionalPosition.Winger:
                    Add(s, RatingSector.CentralDefence, bonus * .480);
                    AddSideOnly(s, p.Side, RatingSector.LeftDefence, RatingSector.RightDefence,
                        bonus * .345);
                    Add(s, RatingSector.Midfield, bonus * .730);
                    AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                        bonus * .375);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    break;

                case RegionalPosition.Forward:
                    // Critical correction: a left/right forward's experience does
                    // NOT appear in the opposite flank attack. A centre forward
                    // influences both sides; a side forward influences only its
                    // own side plus the centre.
                    Add(s, RatingSector.Midfield, bonus * .730);
                    Add(s, RatingSector.CentralAttack, bonus * .450);
                    if (p.Side == PlayerSide.Center)
                    {
                        Add(s, RatingSector.LeftAttack, bonus * .375);
                        Add(s, RatingSector.RightAttack, bonus * .375);
                    }
                    else
                    {
                        AddSideOnly(s, p.Side, RatingSector.LeftAttack, RatingSector.RightAttack,
                            bonus * .375);
                    }
                    break;
            }
        }
    }

    private static double ExperienceBonus(double experience)
    {
        // Hattrick experience contribution table: Wretched +0.40 ... Divine +1.73.
        var values = new[]
        {
            0.00, 0.00, .40, .64, .80, .93, 1.04, 1.13, 1.20, 1.27,
            1.33, 1.39, 1.44, 1.49, 1.53, 1.57, 1.61, 1.64, 1.67, 1.71, 1.73
        };
        var level = Math.Clamp((int)Math.Round(experience), 1, 20);
        return values[level];
    }

    private static void ApplyContext(
        Dictionary<RatingSector, double> s,
        RatingContext c)
    {
        // Midfield venue/attitude/tactic modifiers from the Schum model.
        var midfield = c.MatchLocation switch
        {
            MatchLocation.Home => 1.1989,
            MatchLocation.DerbyAway => 1.1149,
            _ => 1.0
        };

        midfield *= c.Attitude switch
        {
            TeamAttitude.MatchOfTheSeason => 83.0 / 75.0,
            TeamAttitude.PlayItCool => .84,
            _ => 1.0
        };

        if (c.Tactic == TeamTactic.CounterAttack)
            midfield *= .93;

        // Tactic penalties affect the nominal sectors, not the player-level
        // contribution coefficients.
        switch (c.Tactic)
        {
            case TeamTactic.AttackMiddle:
                s[RatingSector.LeftDefence] *= .85;
                s[RatingSector.RightDefence] *= .85;
                break;

            case TeamTactic.AttackWings:
                s[RatingSector.CentralDefence] *= .85;
                break;

            case TeamTactic.Creative:
                s[RatingSector.LeftDefence] *= .93;
                s[RatingSector.CentralDefence] *= .93;
                s[RatingSector.RightDefence] *= .93;
                break;

            case TeamTactic.LongShots:
                s[RatingSector.LeftAttack] *= .96;
                s[RatingSector.CentralAttack] *= .96;
                s[RatingSector.RightAttack] *= .96;
                break;
        }

        // Optional match-minute fatigue. At minute 0 the preview remains full
        // strength; later calls can supply a minute and model stamina decline.
        if (c.MatchMinute > 0)
            ApplyStaminaDecay(s, c);

        s[RatingSector.Midfield] *= midfield;

        // Hattrick's lead/retreat effect is based on the nominal rating and is
        // applied from the original value, not recursively from the last step.
        if (c.GoalDifference >= 2 && !c.IgnoreLeadRetreat)
        {
            var steps = Math.Min(c.GoalDifference - 1, 7);
            var protection = 1.0 + steps * .08;
            var attack = 1.0 - steps * .095;
            s[RatingSector.LeftDefence] *= protection;
            s[RatingSector.CentralDefence] *= protection;
            s[RatingSector.RightDefence] *= protection;
            s[RatingSector.LeftAttack] *= attack;
            s[RatingSector.CentralAttack] *= attack;
            s[RatingSector.RightAttack] *= attack;
        }
    }

    private static void ApplyStaminaDecay(
        Dictionary<RatingSector, double> s,
        RatingContext c)
    {
        // At the moment the caller only has a team-level context, so this is a
        // conservative sector-level curve. Player-specific stamina remains in
        // the RegionalPlayer for the future minute-by-minute calculator.
        var minute = Math.Clamp(c.MatchMinute, 0, 120);
        var t = minute / 90.0;
        var factor = 1.0 - .10 * Math.Clamp(t, 0, 1);
        s[RatingSector.Midfield] *= factor;
    }

    private static double LoyaltyEffect(double loyalty)
    {
        if (loyalty <= 0) return 0;
        return Math.Clamp(loyalty / 19.0, 0.0, 1.5);
    }

    private static void AddBothSides(
        Dictionary<RatingSector, double> s,
        RatingSector left,
        RatingSector right,
        double value)
    {
        s[left] += value;
        s[right] += value;
    }

    private static void AddSideOnly(
        Dictionary<RatingSector, double> s,
        PlayerSide side,
        RatingSector left,
        RatingSector right,
        double value)
    {
        if (side == PlayerSide.Left)
            s[left] += value;
        else if (side == PlayerSide.Right)
            s[right] += value;
        else
            AddBothSides(s, left, right, value);
    }

    private static void Add(
        Dictionary<RatingSector, double> s,
        RatingSector sector,
        double value)
        => s[sector] += value;

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

        var side = slot.Code.EndsWith("-L", StringComparison.Ordinal)
            ? PlayerSide.Left
            : slot.Code.EndsWith("-R", StringComparison.Ordinal)
                ? PlayerSide.Right
                : PlayerSide.Center;

        return new RegionalPlayer(
            p.Id,
            position,
            side,
            slot.Order,
            p.Keeper,
            p.Defending,
            p.Playmaking,
            p.Passing,
            p.Winger,
            p.Scoring,
            p.Form,
            p.Loyalty,
            p.Experience,
            p.Stamina);
    }

    private readonly record struct OrderMatrix(
        double CentralDefence,
        double SideDefence,
        double Midfield,
        double SidePassing,
        double CenterPassing,
        double CenterScoring,
        double SideWinger);

    private readonly record struct WingerMatrix(
        double CentralDefence,
        double SideDefence,
        double Midfield,
        double SidePassing,
        double SideWinger,
        double CenterPassing);
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

public enum RegionalPosition
{
    Goalkeeper,
    CentralDefender,
    WingBack,
    InnerMidfielder,
    Winger,
    Forward
}

public enum PlayerOrder
{
    Normal,
    Defensive,
    Offensive,
    TowardsWing,
    TowardsMiddle
}

public enum PlayerSide
{
    Left,
    Center,
    Right
}

public enum MatchLocation
{
    Away,
    Home,
    DerbyAway
}

public enum TeamAttitude
{
    Normal,
    MatchOfTheSeason,
    PlayItCool
}

public enum TeamTactic
{
    Normal,
    CounterAttack,
    LongShots,
    AttackMiddle,
    AttackWings,
    Creative
}

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
    double Experience,
    double Stamina);

public sealed record RatingContext(
    MatchLocation MatchLocation,
    TeamAttitude Attitude,
    TeamTactic Tactic)
{
    public int MatchMinute { get; init; }
    public int GoalDifference { get; init; }
    public bool IgnoreLeadRetreat { get; init; }

    public static RatingContext Default => new(
        MatchLocation.Away,
        TeamAttitude.Normal,
        TeamTactic.Normal);
}

public sealed record RegionalRatingPair(
    RegionalRatingSnapshot Own,
    RegionalRatingSnapshot Opponent)
{
    public IReadOnlyList<double> OwnSeven => new[]
    {
        Own.LeftDefence,
        Own.CentralDefence,
        Own.RightDefence,
        Own.Midfield,
        Own.LeftAttack,
        Own.CentralAttack,
        Own.RightAttack
    };

    public IReadOnlyList<double> OpponentSeven => new[]
    {
        Opponent.LeftDefence,
        Opponent.CentralDefence,
        Opponent.RightDefence,
        Opponent.Midfield,
        Opponent.LeftAttack,
        Opponent.CentralAttack,
        Opponent.RightAttack
    };
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
