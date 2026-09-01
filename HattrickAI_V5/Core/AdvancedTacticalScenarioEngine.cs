using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M7.2 tactical metadata layer.
/// It does not mutate the seven regional ratings. It derives bounded tactical
/// effectiveness and an explicit M8 handoff contract from the candidate.
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
            return Empty(state, opponentAverageMainSkill);

        var totalPassing = outfield.Sum(p => Math.Max(0, p.Passing));
        var totalDefending = outfield.Sum(p => Math.Max(0, p.Defending));
        var totalPlaymaking = outfield.Sum(p => Math.Max(0, p.Playmaking));
        var totalScoring = outfield.Sum(p => Math.Max(0, p.Scoring));
        var totalWinger = outfield.Sum(p => Math.Max(0, p.Winger));
        var totalStamina = outfield.Sum(p => Math.Max(0, p.Stamina));
        var totalExperience = outfield.Sum(p => Math.Max(0, p.Experience));

        var tactic = Map(state.TeamTactic);
        var tacticSkill = CalculateTacticSkill(
            tactic, totalPassing, totalDefending, totalScoring,
            totalStamina, totalExperience, outfield.Length);

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

        var inputs = new TacticalInputTotals(
            totalPassing, totalDefending, totalPlaymaking, totalScoring,
            totalWinger, totalStamina, totalExperience);

        var m8 = BuildM8Context(
            state, tactic, level, distribution, inputs, pressure, counter, longShots, creative);

        return new AdvancedTacticalScenarioResult(
            state.CandidateId, tactic, tacticSkill, level, inputs,
            distribution, pressure, counter, longShots, creative,
            opponentAverageMainSkill,
            CalibrationStatus.ResearchBackedStructureNeedsMatchCalibration,
            m8);
    }

    /// <summary>
    /// Creates the canonical M8 handoff by combining M7's regional rating with
    /// M7.2's tactical context. M8 can therefore compare candidates without
    /// knowing how either rating or tactic metadata was internally calculated.
    /// </summary>
    public static M8TacticalMatchupInput BuildM8Input(
        RatingScenarioResult m7,
        AdvancedTacticalScenarioResult m72)
    {
        ArgumentNullException.ThrowIfNull(m7);
        ArgumentNullException.ThrowIfNull(m72);

        if (!string.Equals(m7.State.CandidateId, m72.CandidateId, StringComparison.Ordinal))
            throw new ArgumentException("M7 and M7.2 CandidateId values must match.");

        return new M8TacticalMatchupInput(
            m72.CandidateId,
            m7.State.FormationId,
            m7.State.LineupId,
            m7.State.BehaviourSetId,
            m7.Rating,
            m7.State.MatchLocation,
            m7.State.TeamAttitude,
            m7.State.TeamSpirit,
            m72.Tactic,
            m72.Level,
            m72.ChanceDistribution,
            m72.Pressing,
            m72.CounterAttack,
            m72.LongShots,
            m72.PlayCreatively,
            m72.Inputs,
            m72.CalibrationStatus,
            m7.Confidence);
    }

    private static AdvancedTacticalScenarioResult Empty(MatchState state, double opponentAverageMainSkill)
    {
        var level = new TacticalLevel("None", 0);
        var tactic = AdvancedTactic.Normal;
        var inputs = new TacticalInputTotals(0, 0, 0, 0, 0, 0, 0);
        var distribution = ChanceDistributionEffect.For(tactic, level);
        var m8 = BuildM8Context(state, tactic, level, distribution, inputs, null, null, null, null);

        return new AdvancedTacticalScenarioResult(
            state.CandidateId, tactic, 0, level, inputs, distribution,
            null, null, null, null, opponentAverageMainSkill,
            CalibrationStatus.ResearchBackedStructureNeedsMatchCalibration,
            m8);
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
        if (outfieldCount <= 0) return 0;

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

    private static M8TacticalContext BuildM8Context(
        MatchState state,
        AdvancedTactic tactic,
        TacticalLevel level,
        ChanceDistributionEffect distribution,
        TacticalInputTotals inputs,
        TacticalPressureProfile? pressure,
        CounterAttackProfile? counter,
        LongShotsProfile? longShots,
        CreativeProfile? creative)
        => new(
            state.CandidateId,
            tactic,
            level,
            distribution,
            pressure,
            counter,
            longShots,
            creative,
            inputs,
            state.MatchLocation,
            state.TeamAttitude,
            state.TeamSpirit,
            state.MatchMinute,
            state.GoalDifference);
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
    CalibrationStatus CalibrationStatus,
    M8TacticalContext M8Context);

/// <summary>
/// Canonical tactical payload handed from M7.2 to M8.
/// M8 compares this payload against the opponent; M7.2 does not select a winner.
/// </summary>
public sealed record M8TacticalContext(
    string CandidateId,
    AdvancedTactic Tactic,
    TacticalLevel Level,
    ChanceDistributionEffect ChanceDistribution,
    TacticalPressureProfile? Pressing,
    CounterAttackProfile? CounterAttack,
    LongShotsProfile? LongShots,
    CreativeProfile? PlayCreatively,
    TacticalInputTotals Inputs,
    MatchLocation MatchLocation,
    TeamAttitude TeamAttitude,
    double TeamSpirit,
    int MatchMinute,
    int GoalDifference);

/// <summary>
/// Complete M7 + M7.2 candidate payload that M8 can consume directly.
/// Opponent data is intentionally not embedded here; M8 receives the opponent
/// candidate/reference separately so own and opponent provenance remain clear.
/// </summary>
public sealed record M8TacticalMatchupInput(
    string CandidateId,
    string FormationId,
    string LineupId,
    string BehaviourSetId,
    RegionalRatingSnapshot OwnRating,
    MatchLocation MatchLocation,
    TeamAttitude TeamAttitude,
    double TeamSpirit,
    AdvancedTactic Tactic,
    TacticalLevel TacticalLevel,
    ChanceDistributionEffect ChanceDistribution,
    TacticalPressureProfile? Pressing,
    CounterAttackProfile? CounterAttack,
    LongShotsProfile? LongShots,
    CreativeProfile? PlayCreatively,
    TacticalInputTotals TacticalInputs,
    CalibrationStatus CalibrationStatus,
    RatingConfidence RatingConfidence);

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
                0.25, 0.35, 0.25, 0.15,
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
