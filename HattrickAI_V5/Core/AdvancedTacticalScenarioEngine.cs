using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// V5.2 / M7.2 tactical metadata layer.
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

        var tacticSkill = state.TeamTactic switch
        {
            TeamTactic.AttackMiddle => totalPassing,
            TeamTactic.AttackWings => totalPassing,
            TeamTactic.CounterAttack => totalDefending + (2.0 * totalPassing),
            TeamTactic.Creative => (4.0 * totalPassing) + totalExperience,
            TeamTactic.LongShots => totalScoring + totalPassing,
            TeamTactic.Pressing => totalDefending + totalStamina,
            _ => 0d
        };

        var level = TacticalLevel.FromAggregate(state.TeamTactic, tacticSkill, opponentAverageMainSkill);
        var distribution = ChanceDistribution.For(state.TeamTactic, level);
        var pressure = state.TeamTactic == TeamTactic.Pressing
            ? new TacticalPressureProfile(totalDefending, totalStamina, level.Value)
            : null;
        var counter = state.TeamTactic == TeamTactic.CounterAttack
            ? new CounterAttackProfile(totalDefending, totalPassing, level.Value)
            : null;
        var longShots = state.TeamTactic == TeamTactic.LongShots
            ? new LongShotsProfile(totalScoring, totalPassing, level.Value)
            : null;
        var creative = state.TeamTactic == TeamTactic.Creative
            ? new CreativeProfile(totalPassing, totalExperience, level.Value)
            : null;

        return new AdvancedTacticalScenarioResult(
            state.CandidateId,
            state.TeamTactic,
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
}

public sealed record AdvancedTacticalScenarioResult(
    string CandidateId,
    TeamTactic Tactic,
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
    public static TacticalLevel FromAggregate(TeamTactic tactic, double aggregate, double opponentAverageMainSkill)
    {
        if (tactic == TeamTactic.Normal || aggregate <= 0)
            return new TacticalLevel("None", 0);

        // Conservative internal normalization. This is not presented as an
        // official Hattrick tactic-level table; it only keeps candidate scores
        // comparable until historical CHPP calibration is available.
        var scale = tactic switch
        {
            TeamTactic.AttackMiddle or TeamTactic.AttackWings => 7.5,
            TeamTactic.CounterAttack => 10.0,
            TeamTactic.Pressing => 10.0,
            TeamTactic.Creative => 25.0,
            TeamTactic.LongShots => 10.0,
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
    public static ChanceDistributionEffect For(TeamTactic tactic, TacticalLevel level)
    {
        // Regular-chance baseline commonly used in the current reference
        // material: centre 35%, each wing 25%; set pieces are tracked separately.
        // For AIM/AOW we expose a bounded *directional* shift rather than a
        // fabricated exact game-engine formula. The shift is intended for M8/M9.
        var shift = Math.Clamp(0.15 + (0.30 - 0.15) * (level.Value / 10.0), 0.15, 0.30);

        return tactic switch
        {
            TeamTactic.AttackMiddle => new ChanceDistributionEffect(
                0.25 - (0.25 * shift),
                0.35 + (0.50 * shift),
                0.25 - (0.25 * shift),
                0.15,
                "AIM shifts regular chances from wings towards centre; exact conversion awaits calibration."),
            TeamTactic.AttackWings => new ChanceDistributionEffect(
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
