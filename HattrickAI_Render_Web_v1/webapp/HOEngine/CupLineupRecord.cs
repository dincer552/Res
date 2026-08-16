using HattrickAI.CHPP;

namespace HattrickAI.HOEngine;

/// <summary>
/// Immutable historical snapshot of the user's latest standard cup match.
/// This is deliberately separate from the recommended/league lineup model.
/// </summary>
public sealed record CupLineupRecord(
    ChppFixture Fixture,
    int TeamId,
    string TeamName,
    TeamData TeamData,
    string Formation,
    IReadOnlyList<ChppLineupPlayer> Players,
    string Source = "CHPP_MATCHLINEUP_HISTORICAL_CUP");
