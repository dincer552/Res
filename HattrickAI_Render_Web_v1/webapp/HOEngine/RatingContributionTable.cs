using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

public enum PlayerRole
{
    Goalkeeper,
    LeftDefender,
    CentralDefender,
    RightDefender,
    LeftMidfielder,
    CentralMidfielder,
    RightMidfielder,
    LeftWinger,
    RightWinger,
    LeftForward,
    CentralForward,
    RightForward
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

public enum PlayerBehaviour
{
    Normal,
    Offensive,
    Defensive,
    TowardsMiddle,
    TowardsWing
}

public enum MatchWeather
{
    Normal,
    Rainy,
    Sunny
}

public sealed class RatingContributionTable
{
    private enum ContributionSet { SideDefence, CentralDefence, Midfield, SideAttack, CentralAttack }
    private enum LineupSector { Goal, CentralDefence, Back, InnerMidfield, Wing, Forward }
    private enum Side { Left, Middle, Right }
    private enum SideRestriction { None, ThisSideOnly, MiddleOnly, OppositeSideOnly }

    private sealed record Rule(
        ContributionSet Set,
        string Skill,
        LineupSector Sector,
        SideRestriction Restriction,
        PlayerBehaviour Behaviour,
        double Factor,
        string? Specialty = null);

    private readonly List<Rule> _rules = BuildRules();

    public double GetContribution(
        PlayerData player,
        PlayerRole role,
        RatingSector ratingSector,
        PlayerBehaviour behaviour,
        PlayerRatingCalculator calculator)
    {
        if (player.Injured || player.Suspended)
            return 0;

        var definition = DefineRole(role);
        var parameter = ratingSector switch
        {
            RatingSector.LeftDefence => (ContributionSet.SideDefence, Side.Left),
            RatingSector.CentralDefence => (ContributionSet.CentralDefence, Side.Middle),
            RatingSector.RightDefence => (ContributionSet.SideDefence, Side.Right),
            RatingSector.Midfield => (ContributionSet.Midfield, Side.Middle),
            RatingSector.LeftAttack => (ContributionSet.SideAttack, Side.Left),
            RatingSector.CentralAttack => (ContributionSet.CentralAttack, Side.Middle),
            RatingSector.RightAttack => (ContributionSet.SideAttack, Side.Right),
            _ => throw new ArgumentOutOfRangeException(nameof(ratingSector))
        };

        double total = 0;
        string specialty = NormalizeSpecialty(player.Specialty);

        foreach (var group in _rules
            .Where(r => r.Set == parameter.Item1 &&
                        r.Sector == definition.Sector &&
                        r.Behaviour == behaviour &&
                        RestrictionAllows(definition.Side, parameter.Item2, r.Restriction))
            .GroupBy(r => new { r.Skill, r.Sector, r.Restriction, r.Behaviour }))
        {
            var specialtyRule = group.FirstOrDefault(r =>
                r.Specialty != null &&
                string.Equals(r.Specialty, specialty, StringComparison.OrdinalIgnoreCase));

            var rule = specialtyRule ?? group.FirstOrDefault(r => r.Specialty == null);
            if (rule != null)
                total += calculator.GetSkill(player, rule.Skill) * rule.Factor;
        }

        return total;
    }

    public static (string Sector, string Side) DescribeRole(PlayerRole role)
    {
        var d = DefineRole(role);
        return (d.Sector.ToString(), d.Side.ToString());
    }

    private static (LineupSector Sector, Side Side) DefineRole(PlayerRole role) => role switch
    {
        PlayerRole.Goalkeeper => (LineupSector.Goal, Side.Middle),
        PlayerRole.LeftDefender => (LineupSector.Back, Side.Left),
        PlayerRole.CentralDefender => (LineupSector.CentralDefence, Side.Middle),
        PlayerRole.RightDefender => (LineupSector.Back, Side.Right),
        PlayerRole.LeftMidfielder => (LineupSector.InnerMidfield, Side.Left),
        PlayerRole.CentralMidfielder => (LineupSector.InnerMidfield, Side.Middle),
        PlayerRole.RightMidfielder => (LineupSector.InnerMidfield, Side.Right),
        PlayerRole.LeftWinger => (LineupSector.Wing, Side.Left),
        PlayerRole.RightWinger => (LineupSector.Wing, Side.Right),
        PlayerRole.LeftForward => (LineupSector.Forward, Side.Left),
        PlayerRole.CentralForward => (LineupSector.Forward, Side.Middle),
        PlayerRole.RightForward => (LineupSector.Forward, Side.Right),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static bool RestrictionAllows(Side playerSide, Side targetSide, SideRestriction restriction) => restriction switch
    {
        SideRestriction.None => true,
        SideRestriction.ThisSideOnly => playerSide == targetSide,
        SideRestriction.MiddleOnly => playerSide == Side.Middle,
        SideRestriction.OppositeSideOnly =>
            (targetSide == Side.Left && playerSide == Side.Right) ||
            (targetSide == Side.Right && playerSide == Side.Left),
        _ => false
    };

    private static string NormalizeSpecialty(string value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim() switch
        {
            "Teknik" or "Teknikçi" => "Technical",
            "Güçlü" => "Powerful",
            "Hızlı" => "Quick",
            "Kafa" => "Head",
            "Öngörülemez" => "Unpredictable",
            "Rejeneratif" => "Regainer",
            "Destek" => "Support",
            _ => value.Trim()
        };

    private static List<Rule> BuildRules()
    {
        var rules = new List<Rule>();
        void All(ContributionSet set, string skill, LineupSector sector, SideRestriction restriction, PlayerBehaviour behaviour, double factor)
            => rules.Add(new Rule(set, skill, sector, restriction, behaviour, factor));
        void Specialty(ContributionSet set, string skill, LineupSector sector, SideRestriction restriction, PlayerBehaviour behaviour, string specialty, double factor)
            => rules.Add(new Rule(set, skill, sector, restriction, behaviour, factor, specialty));

        // Values below are the public HO! RatingPredictionModel contribution parameters,
        // translated into an independent C# data table. The model treats all specialties
        // equally for these entries; explicit specialty overrides are added separately.
        All(ContributionSet.SideDefence,"keeper",LineupSector.Goal,SideRestriction.None,PlayerBehaviour.Normal,.61);
        All(ContributionSet.SideDefence,"defending",LineupSector.Goal,SideRestriction.None,PlayerBehaviour.Normal,.25);
        All(ContributionSet.SideDefence,"defending",LineupSector.CentralDefence,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.52);
        All(ContributionSet.SideDefence,"defending",LineupSector.CentralDefence,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.40);
        All(ContributionSet.SideDefence,"defending",LineupSector.CentralDefence,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.81);
        All(ContributionSet.SideDefence,"defending",LineupSector.CentralDefence,SideRestriction.MiddleOnly,PlayerBehaviour.Normal,.26);
        All(ContributionSet.SideDefence,"defending",LineupSector.CentralDefence,SideRestriction.MiddleOnly,PlayerBehaviour.Offensive,.20);
        All(ContributionSet.SideDefence,"defending",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.92);
        All(ContributionSet.SideDefence,"defending",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.74);
        All(ContributionSet.SideDefence,"defending",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,1.00);
        All(ContributionSet.SideDefence,"defending",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsMiddle,.75);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.19);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.09);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.27);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.24);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Normal,.095);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Offensive,.045);
        All(ContributionSet.SideDefence,"defending",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Defensive,.135);
        All(ContributionSet.SideDefence,"defending",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.35);
        All(ContributionSet.SideDefence,"defending",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.22);
        All(ContributionSet.SideDefence,"defending",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.61);
        All(ContributionSet.SideDefence,"defending",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsMiddle,.29);

        All(ContributionSet.CentralDefence,"keeper",LineupSector.Goal,SideRestriction.None,PlayerBehaviour.Normal,.87);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Goal,SideRestriction.None,PlayerBehaviour.Normal,.35);
        All(ContributionSet.CentralDefence,"defending",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.Normal,1.00);
        All(ContributionSet.CentralDefence,"defending",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.Offensive,.73);
        All(ContributionSet.CentralDefence,"defending",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.TowardsWing,.67);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Normal,.38);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Offensive,.35);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Defensive,.43);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Back,SideRestriction.None,PlayerBehaviour.TowardsMiddle,.70);
        All(ContributionSet.CentralDefence,"defending",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Normal,.40);
        All(ContributionSet.CentralDefence,"defending",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Offensive,.16);
        All(ContributionSet.CentralDefence,"defending",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Defensive,.58);
        All(ContributionSet.CentralDefence,"defending",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.TowardsWing,.33);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Normal,.20);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Offensive,.13);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Defensive,.25);
        All(ContributionSet.CentralDefence,"defending",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.TowardsMiddle,.25);

        All(ContributionSet.Midfield,"playmaking",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.Normal,.25);
        All(ContributionSet.Midfield,"playmaking",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.Offensive,.40);
        All(ContributionSet.Midfield,"playmaking",LineupSector.CentralDefence,SideRestriction.None,PlayerBehaviour.TowardsWing,.15);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Normal,.15);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Offensive,.20);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Back,SideRestriction.None,PlayerBehaviour.Defensive,.10);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Back,SideRestriction.None,PlayerBehaviour.TowardsMiddle,.20);
        All(ContributionSet.Midfield,"playmaking",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Normal,1.00);
        All(ContributionSet.Midfield,"playmaking",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Offensive,.95);
        All(ContributionSet.Midfield,"playmaking",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Defensive,.95);
        All(ContributionSet.Midfield,"playmaking",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.TowardsWing,.90);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Normal,.45);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Offensive,.30);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Defensive,.30);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.TowardsMiddle,.55);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,.25);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.35);
        All(ContributionSet.Midfield,"playmaking",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.TowardsWing,.15);

        All(ContributionSet.CentralAttack,"passing",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Normal,.33);
        All(ContributionSet.CentralAttack,"passing",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Offensive,.49);
        All(ContributionSet.CentralAttack,"passing",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Defensive,.18);
        All(ContributionSet.CentralAttack,"passing",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.TowardsWing,.23);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Normal,.11);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Offensive,.13);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.Defensive,.05);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Wing,SideRestriction.None,PlayerBehaviour.TowardsMiddle,.16);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,.33);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.53);
        All(ContributionSet.CentralAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.TowardsWing,.23);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Normal,.22);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Offensive,.31);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.InnerMidfield,SideRestriction.None,PlayerBehaviour.Defensive,.13);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,1.00);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.56);
        All(ContributionSet.CentralAttack,"scoring",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.TowardsWing,.66);

        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Normal,.13);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Offensive,.18);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.MiddleOnly,PlayerBehaviour.Defensive,.07);
        All(ContributionSet.SideAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,.14);
        All(ContributionSet.SideAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.31);
        Specialty(ContributionSet.SideAttack,"passing",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,"Technical",.41);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.26);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.36);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.14);
        All(ContributionSet.SideAttack,"passing",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.31);
        All(ContributionSet.SideAttack,"passing",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.26);
        All(ContributionSet.SideAttack,"passing",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.29);
        All(ContributionSet.SideAttack,"passing",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.21);
        All(ContributionSet.SideAttack,"passing",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsMiddle,.15);
        All(ContributionSet.SideAttack,"passing",LineupSector.Forward,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.21);
        All(ContributionSet.SideAttack,"passing",LineupSector.Forward,SideRestriction.OppositeSideOnly,PlayerBehaviour.TowardsWing,.06);
        All(ContributionSet.SideAttack,"winger",LineupSector.CentralDefence,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.26);
        All(ContributionSet.SideAttack,"winger",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.59);
        All(ContributionSet.SideAttack,"winger",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,.69);
        All(ContributionSet.SideAttack,"winger",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.45);
        All(ContributionSet.SideAttack,"winger",LineupSector.Back,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsMiddle,.35);
        All(ContributionSet.SideAttack,"winger",LineupSector.InnerMidfield,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.59);
        All(ContributionSet.SideAttack,"winger",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Normal,.86);
        All(ContributionSet.SideAttack,"winger",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Offensive,1.00);
        All(ContributionSet.SideAttack,"winger",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.Defensive,.69);
        All(ContributionSet.SideAttack,"winger",LineupSector.Wing,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsMiddle,.74);
        All(ContributionSet.SideAttack,"winger",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,.24);
        All(ContributionSet.SideAttack,"winger",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.13);
        All(ContributionSet.SideAttack,"winger",LineupSector.Forward,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.64);
        All(ContributionSet.SideAttack,"winger",LineupSector.Forward,SideRestriction.OppositeSideOnly,PlayerBehaviour.TowardsWing,.21);
        All(ContributionSet.SideAttack,"scoring",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Normal,.27);
        All(ContributionSet.SideAttack,"scoring",LineupSector.Forward,SideRestriction.None,PlayerBehaviour.Defensive,.13);
        All(ContributionSet.SideAttack,"scoring",LineupSector.Forward,SideRestriction.OppositeSideOnly,PlayerBehaviour.TowardsWing,.19);
        All(ContributionSet.SideAttack,"scoring",LineupSector.Forward,SideRestriction.ThisSideOnly,PlayerBehaviour.TowardsWing,.51);

        return rules;
    }
}
