using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M7.2 tactical metadata layer.
/// It does not mutate the seven regional ratings. It derives a bounded tactical
/// effectiveness value from average outfield skills so a good squad does not
/// automatically become Outstanding+ merely because eleven player skills were
/// summed together.
/// Exact tactic calibration remains dependent on historical CHPP match data.
/// </summary>
public sealed class AdvancedTacticalScenarioEngine
{
    public AdvancedTacticalScenarioResult Calculate(
        IReadOnlyList<RegionalPlayer> players,
        MatchState state,
        double opponentAverageMainSkill = double.NaN)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(state);

        var outfield = players.Where(p => p.Position != RegionalPosition.Goalkeeper).ToArray();
        if (outfield.Length == 0)
        {
            return new AdvancedTacticalScenarioResult(
                state.CandidateId,
                AdvancedTactic.Normal,
                0,
                new TacticalLevel("None", 0),
                new TacticalInputTotals(0, 0, 0, 0, 0, 0, 0),
                ChanceDistributionEffect.For(AdvancedTactic.Normal, new TacticalLevel("None", 0)),
                null,
                null,
                null,
                null,
                opponentAverageMainSkill,
                CalibrationStatus.ResearchBackedStructureNeedsMatchCalibration);
        }

        var totalPassing = outfield.Sum(p => Math.Max(0, p.Passing));
        var totalDefending = outfield.Sum(p => Math.Max(0, p.Defending));
        var totalPlaymaking = outfield.Sum(p => Math.Max(0, p.Playmaking));
        var totalScoring = outfield.Sum(p => Math.Max(0, p.Scoring));
        var totalWinger = outfield.Sum(p => Math.Max(0, p.Winger));
        var totalStamina = outfield.Sum(p => Math.Max(0, p.Stamina));
        var totalExperience = outfield.Sum(p => Math.Max(0, p.Experience));

        var tactic = Map(state.TeamTactic);
        var tacticSkill = CalculateTacticSkill(
            tactic,
            totalPassing,
            totalDefending,
            totalScoring,
            totalStamina,
            totalExperience,
            outfield.Length);

        var level = TacticalLevel.FromAggregate(tactic, tacticSkill, opponentAverageMainSkill);
        var distribution = ChanceDistributionEffect.For(tactic, level);

        var pressure = tactic == AdvancedTactic.Pressing
            ? new TacticalPressureProfile(totalDefending, totalStamina, level.Value)
            : null;
        var counter = tactic == AdvancedTactic.CounterAttack
            ? new CounterAttackProfile(totalDefending, totalPassing, level.Value)
            : null;
        var longShots = tactic == AdvancedTactic.LongShots
            ? new LongShotsProfile(totalScoring, totalPassing, level.Value)
            : null;
        var creative = tactic == AdvancedTactic.Creative
            ? new CreativeProfile(totalPassing, totalExperience, level.Value)
            : null;

        return new AdvancedTacticalScenarioResult(
            state.CandidateId,
            tactic,
            tacticSkill,
            level,
            new TacticalInputTotals(
                totalPassing,
                totalDefending,
                totalPlaymaking,
                totalScoring,
                totalWinger,
                totalStamina,
                totalExperience),
            distribution,
            pressure,
            counter,
            longShots,
            creative,
            opponentAverageMainSkill,
            CalibrationStatus.ResearchBackedStructureNeedsMatchCalibration);
    }

    private static double CalculateTacticSkill(
        AdvancedTactic tactic,
        double passing,
        double defending,
        double scoring,
        double stamina,
        double experience,
        int outfieldCount)
    {
        if (outfieldCount <= 0)
            return 0;

        // Important M7.2 correction:
        // never normalize by the sum of eleven players. Tactic quality is based
        // on squad-level average inputs, then mapped from the 0-20 skill domain
        // into a bounded 0-10 internal effectiveness domain.
        var averagePassing = passing / outfieldCount;
        var averageDefending = defending / outfieldCount;
        var averageScoring = scoring / outfieldCount;
        var averageStamina = stamina / outfieldCount;
        var averageExperience = experience / outfieldCount;

        var skill = tactic switch
        {
            AdvancedTactic.AttackMiddle => averagePassing,
            AdvancedTactic.AttackWings => averagePassing,
            AdvancedTactic.CounterAttack => (averageDefending + averagePassing) / 2.0,
            AdvancedTactic.Creative => (averagePassing + averageExperience) / 2.0,
            AdvancedTactic.LongShots => (averageScoring + averagePassing) / 2.0,
            AdvancedTactic.Pressing => (averageDefending + averageStamina) / 2.0,
            _ => 0d
        };

        return Math.Clamp(skill / 2.0, 0.0, 10.0);
    }

    private static AdvancedTactic Map(TeamTactic tactic) => tactic switch
    {
        TeamTactic.CounterAttack => AdvancedTactic.CounterAttack,
        TeamTactic.LongShots => AdvancedTactic.LongShots,
        TeamTactic.AttackMiddle => AdvancedTactic.AttackMiddle,
        TeamTactic.AttackWings => AdvancedTactic.AttackWings,
        TeamTactic.Creative => AdvancedTactic.Creative,
        _ => AdvancedTactic.Normal
    };
}

public enum AdvancedTactic
{
    Normal,
    Pressing,
    CounterAttack,
    AttackMiddle,
    AttackWings,
    LongShots,
    Creative
}

public sealed record AdvancedTacticalScenarioResult(
    string CandidateId,
    AdvancedTactic Tactic,
    double TacticalSkillAggregate,
    TacticalLevel Level,
    TacticalInputTotals Inputs,
    ChanceDistributionEffect ChanceDistribution,
    TacticalPressureProfile? Pressing,
    CounterAttackProfile? CounterAttack,
    LongShotsProfile? LongShots,
    CreativeProfile? PlayCreatively,
    double OpponentAverageMainSkill,
    CalibrationStatus CalibrationStatus);

public sealed record TacticalInputTotals(
    double TotalPassing,
    double TotalDefending,
    double TotalPlaymaking,
    double TotalScoring,
    double TotalWinger,
    double TotalStamina,
    double TotalExperience);

public sealed record TacticalLevel(string Name, double Value)
{
    public static TacticalLevel FromAggregate(
        AdvancedTactic tactic,
        double aggregate,
        double opponentAverageMainSkill)
    {
        if (tactic == AdvancedTactic.Normal || aggregate <= 0)
            return new TacticalLevel("None", 0);

        var normalized = Math.Clamp(aggregate, 0.0, 10.0);

        // Opponent skill is only a small contextual adjustment. It must not
        // dominate the squad's own tactical input.
        if (!double.IsNaN(opponentAverageMainSkill) && opponentAverageMainSkill > 0)
        {
            var opponentContext = Math.Clamp(opponentAverageMainSkill - 6.0, -2.0, 4.0);
            normalized = Math.Clamp(normalized * (1.0 + opponentContext * 0.015), 0.0, 10.0);
        }

        var name = normalized switch
        {
            < 2 => "Weak",
            < 4 => "Inadequate",
            < 6 => "Passable",
            < 7.5 => "Solid",
            < 9 => "Formidable",
            _ => "Outstanding+"
        };

        return new TacticalLevel(name, normalized);
    }
}

public sealed record ChanceDistributionEffect(
    double LeftShare,
    double CentreShare,
    double RightShare,
    double SetPieceShare,
    string Mechanism)
{
    public static ChanceDistributionEffect For(AdvancedTactic tactic, TacticalLevel level)
    {
        // Keep the existing bounded directional model. These are modelling
        // inputs, not claims about an official exact Hattrick engine formula.
        var shift = Math.Clamp(
            0.15 + (0.30 - 0.15) * (level.Value / 10.0),
            0.15,
            0.30);

        return tactic switch
        {
            AdvancedTactic.AttackMiddle => new ChanceDistributionEffect(
                0.25 - (0.25 * shift),
                0.35 + (0.50 * shift),
                0.25 - (0.25 * shift),
                0.15,
                "AIM shifts regular chances from wings towards centre; exact conversion awaits calibration."),

            AdvancedTactic.AttackWings => new ChanceDistributionEffect(
                0.25 + (0.25 * shift),
                0.35 - (0.50 * shift),
                0.25 + (0.25 * shift),
                0.15,
                "AOW shifts regular chances from centre towards wings; exact conversion awaits calibration."),

            _ => new ChanceDistributionEffect(
                0.25,
                0.35,
                0.25,
                0.15,
                "No directional distribution shift modelled.")
        };
    }
}

public sealed record TacticalPressureProfile(
    double TotalDefending,
    double TotalStamina,
    double TacticalLevel)
{
    public double RelativePressureInput => Math.Max(0, TotalDefending + TotalStamina);
}

public sealed record CounterAttackProfile(
    double TotalDefending,
    double TotalPassing,
    double TacticalLevel)
{
    public double RelativeCounterInput => Math.Max(0, TotalDefending + (2.0 * TotalPassing));
}

public sealed record LongShotsProfile(
    double TotalScoring,
    double TotalPassing,
    double TacticalLevel)
{
    public double ShooterInput => Math.Max(0, TotalScoring);
    public double SupportInput => Math.Max(0, TotalPassing);
}

public sealed record CreativeProfile(
    double TotalPassing,
    double TotalExperience,
    double TacticalLevel)
{
    public double CreativeInput => Math.Max(0, (4.0 * TotalPassing) + TotalExperience);
}

public enum CalibrationStatus
{
    ResearchBackedStructureNeedsMatchCalibration,
    CalibratedAgainstHistoricalMatches
}
