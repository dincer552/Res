using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer.
///
/// PHASE D calibration is based on 60 real historical matches. The observed
/// sample shows that the structural 5 exclusive + 5 open baseline overstates
/// total regular chances (10.0 expected vs 8.8 observed) and materially
/// overstates ownership at the low/midfield-share range.
///
/// Calibrated coefficients are intentionally kept here as explicit constants
/// so they can be regression-tested and replaced when a broader dataset is
/// available. This is a data-derived calibration, not a claim of the hidden
/// Hattrick server formula.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;

    // PHASE D: 60-match calibration dataset.
    // Mean observed total regular chances = 8.8.
    public const double CalibratedTotalRegularChances = 8.8;

    // Observed own-chance ownership regression:
    // ownShare = -0.4380926172 + 1.9561688498 * midfieldShare
    public const double CalibratedOwnershipIntercept = -0.4380926172;
    public const double CalibratedOwnershipMidfieldSlope = 1.9561688498;

    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        ownMidfieldShare = Math.Clamp(ownMidfieldShare, 0.0, 1.0);

        var calibratedOwnOwnership = Math.Clamp(
            CalibratedOwnershipIntercept + CalibratedOwnershipMidfieldSlope * ownMidfieldShare,
            0.0,
            1.0);

        var calibratedOwn = CalibratedTotalRegularChances * calibratedOwnOwnership;
        var calibratedOpponent = CalibratedTotalRegularChances - calibratedOwn;

        // Keep the exclusive/open fields for compatibility and diagnostics.
        // Production expected regular-chance volume now uses the calibrated
        // total + ownership model rather than forcing a 10-chance total.
        var ownOpen = OpenChancePool * ownMidfieldShare;
        var opponentOpen = OpenChancePool - ownOpen;

        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            ExclusiveChancesPerTeam,
            OpenChancePool,
            ownOpen,
            opponentOpen,
            calibratedOwn,
            calibratedOpponent,
            "Phase D calibrated: total=8.8; ownShare=-0.4380926172+1.9561688498*midfieldShare (60-match sample).");
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
