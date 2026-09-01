using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// V5.1 M7.2 tactical metadata layer.
/// It deliberately does not mutate the seven regional ratings. Instead it
/// computes tactic-specific inputs and conservative, explainable effects that
/// M8/M9 can consume later. Exact tactic calibration remains a separate step
/// until enough real CHPP matchdetails data is available.
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
        var totalPassing = outfield.Sum(p => Math.Max(0, p.Passing));
        var totalDefending = outfield.Sum(p => Math.Max(0, p.Defending));
        var totalPlaymaking = outfield.Sum(p => Math.Max(0, p.Playmaking));
        var totalScoring = outfield.Sum(p => Math.Max(0, p.Scoring));
        var totalWinger = outfield.Sum(p => Math.Max(0, p.Winger));
        var totalStamina = outfield.Sum(p => Math.Max(0, p.Stamina));
        var totalExperience = outfield.Sum(p => Math.Max(0, p.Experience));

        var tactic = Map(state.TeamTactic);
        var tacticSkill = CalculateTacticSkill(tactic, totalPassing, totalDefending, totalScoring, totalStamina, totalExperience);
        var level = TacticalLevel.FromAggregate(tactic, tacticSkill, opponentAverageMainSkill);
        var distribution = ChanceDistribution.For(tactic, level);
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
            new TacticalInputTotals(totalPassing, totalDefending, totalPlaymaking, totalScoring, totalWinger, totalStamina, totalExperience),
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
        double experience)
    {
        return tactic switch
        {
            AdvancedTactic.AttackMiddle => passing,
            AdvancedTactic.AttackWings => passing,
            AdvancedTactic.CounterAttack => defending + (2.0 * passing),
            AdvancedTactic.Creative => (4.0 * passing) + experience,
            AdvancedTactic.LongShots => scoring + passing,
            AdvancedTactic.Pressing => defending + stamina,
            _ => 0d
        };
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

/// <summary>
/// Tactic effectiveness level. The level is intentionally represented as an
/// ordinal/value pair; the exact division thresholds remain calibration data.
/// </summary>
public sealed record TacticalLevel(string Name, double Value)
{
    public static TacticalLevel FromAggregate(AdvancedTactic tactic, double aggregate, double opponentAverageMainSkill)
    {
        if (tactic == AdvancedTactic.Normal || aggregate <= 0)
            return new TacticalLevel("None", 0);

        // Conservative internal normalization. This is not presented as an
        // official Hattrick tactic-level table; it only keeps candidate scores
        // comparable until historical CHPP calibration is available.
        var scale = tactic switch
        {
            AdvancedTactic.AttackMiddle or AdvancedTactic.AttackWings => 7.5,
            AdvancedTactic.CounterAttack => 10.0,
            AdvancedTactic.Pressing => 10.0,
            AdvancedTactic.Creative => 25.0,
            AdvancedTactic.LongShots => 10.0,
            _ => 10.0
        };

        var normalized = Math.Clamp(aggregate / scale, 0.0, 10.0);
        if (!double.IsNaN(opponentAverageMainSkill) && opponentAverageMainSkill > 0)
            normalized = Math.Clamp(normalized * (1.0 + Math.Clamp(opponentAverageMainSkill - 6.0, -2.0, 4.0) * 0.015), 0.0, 10.0);

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
        // Common reference baseline: centre 35%, each wing 25%, set pieces 15%.
        // AIM/AOW are represented as bounded directional shifts only; the
        // exact engine conversion remains deliberately uncalibrated.
        var shift = Math.Clamp(0.15 + (0.30 - 0.15) * (level.Value / 10.0), 0.15, 0.30);

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
            _ => new ChanceDistributionEffect(0.25, 0.35, 0.25, 0.15, "No directional distribution shift modelled.")
        };
    }
}

public sealed record TacticalPressureProfile(double TotalDefending, double TotalStamina, double TacticalLevel)
{
    public double RelativePressureInput => Math.Max(0, TotalDefending + TotalStamina);
}

public sealed record CounterAttackProfile(double TotalDefending, double TotalPassing, double TacticalLevel)
{
    public double RelativeCounterInput => Math.Max(0, TotalDefending + (2.0 * TotalPassing));
}

public sealed record LongShotsProfile(double TotalScoring, double TotalPassing, double TacticalLevel)
{
    public double ShooterInput => Math.Max(0, TotalScoring);
    public double SupportInput => Math.Max(0, TotalPassing);
}

public sealed record CreativeProfile(double TotalPassing, double TotalExperience, double TacticalLevel)
{
    public double CreativeInput => Math.Max(0, (4.0 * TotalPassing) + TotalExperience);
}

public enum CalibrationStatus
{
    ResearchBackedStructureNeedsMatchCalibration,
    CalibratedAgainstHistoricalMatches
}
