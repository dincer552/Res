using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer.
/// The previous M8 model treated chance ownership as a continuous midfield-share
/// multiplier. This layer introduces the researched structural concept of
/// exclusive + open regular chances while deliberately keeping the total-count
/// formula calibration-neutral.
///
/// The current structural baseline is 5 exclusive chances per team plus 5 open
/// chances. Open chances are allocated by midfield share. Historical calibration
/// will later determine whether the live engine needs a variable total pool.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;

    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        ownMidfieldShare = Math.Clamp(ownMidfieldShare, 0.0, 1.0);

        var ownOpen = OpenChancePool * ownMidfieldShare;
        var opponentOpen = OpenChancePool - ownOpen;

        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            ExclusiveChancesPerTeam,
            OpenChancePool,
            ownOpen,
            opponentOpen,
            ExclusiveChancesPerTeam + ownOpen,
            ExclusiveChancesPerTeam + opponentOpen,
            "Structural baseline: 5 exclusive + 5 open; total-chance calibration pending.");
    }
}

public sealed record DiscreteChanceAllocation(
    int OwnExclusive,
    int OpponentExclusive,
    int OpenChancePool,
    double OwnOpenExpected,
    double OpponentOpenExpected,
    double OwnRegularChanceExpected,
    double OpponentRegularChanceExpected,
    string Mechanism);
