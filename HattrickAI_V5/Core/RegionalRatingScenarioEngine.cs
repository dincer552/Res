using System;
using System.Collections.Generic;

namespace HattrickAI.V5.Core;

/// <summary>
/// V5.1 M7 scenario layer.
/// Keeps the proven RegionalRatingEngine contribution model intact while adding
/// an explicit match-state contract and Team Spirit as a midfield context input.
/// M7 evaluates a complete scenario; it does not choose the scenario.
/// </summary>
public sealed class RegionalRatingScenarioEngine
{
    private readonly RegionalRatingEngine _baseEngine = new();

    public RatingScenarioResult Calculate(
        IReadOnlyList<RegionalPlayer> players,
        MatchState state)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(state);

        var context = new RatingContext(state.MatchLocation, state.TeamAttitude, state.TeamTactic)
        {
            MatchMinute = state.MatchMinute,
            GoalDifference = state.GoalDifference,
            IgnoreLeadRetreat = state.IgnoreLeadRetreat
        };

        var baseRating = _baseEngine.Calculate(players, context);
        var adjusted = ApplyTeamSpirit(baseRating, state.TeamSpirit);

        return new RatingScenarioResult(
            adjusted,
            state,
            RatingConfidence.High,
            new RatingModifiers(
                TeamSpiritMultiplier(state.TeamSpirit),
                state.MatchLocation,
                state.TeamAttitude,
                state.TeamTactic));
    }

    public RatingScenarioResult CalculateLineup(
        Lineup lineup,
        IReadOnlyList<Player> players,
        MatchState state)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(state);

        var context = new RatingContext(state.MatchLocation, state.TeamAttitude, state.TeamTactic)
        {
            MatchMinute = state.MatchMinute,
            GoalDifference = state.GoalDifference,
            IgnoreLeadRetreat = state.IgnoreLeadRetreat
        };

        var baseRating = _baseEngine.CalculateLineup(lineup, players, context);
        var adjusted = ApplyTeamSpirit(baseRating, state.TeamSpirit);

        return new RatingScenarioResult(
            adjusted,
            state,
            RatingConfidence.High,
            new RatingModifiers(
                TeamSpiritMultiplier(state.TeamSpirit),
                state.MatchLocation,
                state.TeamAttitude,
                state.TeamTactic));
    }

    /// <summary>
    /// Team Spirit affects midfield only. The legacy engine already applies
    /// venue/attitude/tactic effects, so this layer adds the TS component only.
    /// The sqrt curve is consistent with the existing V1 engine and with the
    /// published TS reference table; composed (4.5) is approximately 1.0.
    /// </summary>
    public static double TeamSpiritMultiplier(double teamSpirit)
    {
        if (teamSpirit <= 0) return 1.0;
        return 0.10 + 0.425 * Math.Sqrt(Math.Clamp(teamSpirit, 0.0, 10.0));
    }

    private static RegionalRatingSnapshot ApplyTeamSpirit(
        RegionalRatingSnapshot rating,
        double teamSpirit)
    {
        var factor = TeamSpiritMultiplier(teamSpirit);
        var rawMidfield = rating.RawMidfield * factor;

        return new RegionalRatingSnapshot(
            rating.RawLeftDefence,
            rating.RawCentralDefence,
            rating.RawRightDefence,
            rawMidfield,
            rating.RawLeftAttack,
            rating.RawCentralAttack,
            rating.RawRightAttack,
            RegionalRatingEngine.Display(rating.RawLeftDefence),
            RegionalRatingEngine.Display(rating.RawCentralDefence),
            RegionalRatingEngine.Display(rating.RawRightDefence),
            RegionalRatingEngine.Display(rawMidfield),
            RegionalRatingEngine.Display(rating.RawLeftAttack),
            RegionalRatingEngine.Display(rating.RawCentralAttack),
            RegionalRatingEngine.Display(rating.RawRightAttack));
    }
}

/// <summary>
/// Complete state used by M7 to simulate one candidate match scenario.
/// Candidate selection remains outside M7 (M6/M6B/M10).
/// </summary>
public sealed record MatchState(
    string CandidateId,
    string FormationId,
    string LineupId,
    string BehaviourSetId,
    MatchLocation MatchLocation,
    TeamAttitude TeamAttitude,
    TeamTactic TeamTactic,
    double TeamSpirit)
{
    public int MatchMinute { get; init; }
    public int GoalDifference { get; init; }
    public bool IgnoreLeadRetreat { get; init; }
    public double Confidence { get; init; } = 1.0;
}

public enum RatingConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

public sealed record RatingModifiers(
    double TeamSpiritMultiplier,
    MatchLocation MatchLocation,
    TeamAttitude TeamAttitude,
    TeamTactic TeamTactic);

public sealed record RatingScenarioResult(
    RegionalRatingSnapshot Rating,
    MatchState State,
    RatingConfidence Confidence,
    RatingModifiers Modifiers);
