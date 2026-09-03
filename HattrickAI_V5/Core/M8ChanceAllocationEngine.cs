using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer.
/// PHASE D calibration is based on 60 real historical matches.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;

    // Phase D: 60-match calibration.
    public const double CalibratedTotalRegularChances = 8.8;
    public const double CalibratedOwnershipIntercept = -0.4380926172;
    public const double CalibratedOwnershipMidfieldSlope = 1.9561688498;

    // Phase E: observed own normal-chance sector distribution from the same
    // calibration set (292 own regular sector chances).
    public const double CalibratedLeftSectorShare = 0.2568;
    public const double CalibratedCentreSectorShare = 0.4281;
    public const double CalibratedRightSectorShare = 0.3151;

    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        ownMidfieldShare = Math.Clamp(ownMidfieldShare, 0.0, 1.0);

        var calibratedOwnOwnership = Math.Clamp(
            CalibratedOwnershipIntercept + CalibratedOwnershipMidfieldSlope * ownMidfieldShare,
            0.0,
            1.0);

        var calibratedOwn = CalibratedTotalRegularChances * calibratedOwnOwnership;
        var calibratedOpponent = CalibratedTotalRegularChances - calibratedOwn;

        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            OpponentExclusive: ExclusiveChancesPerTeam,
            OpenChancePool,
            OwnOpenExpected: OpenChancePool * ownMidfieldShare,
            OpponentOpenExpected: OpenChancePool - (OpenChancePool * ownMidfieldShare),
            OwnRegularChanceExpected: calibratedOwn,
            OpponentRegularChanceExpected: calibratedOpponent,
            "Phase E calibrated: total=8.8; ownership=-0.4380926172+1.9561688498*midfieldShare; sectors=25.68/42.81/31.51.");
    }

    public static (double Left, double Centre, double Right) CalculateSectorShares(
        AdvancedTactic tactic = AdvancedTactic.Normal,
        double tacticStrength = 0.0)
    {
        var left = CalibratedLeftSectorShare;
        var centre = CalibratedCentreSectorShare;
        var right = CalibratedRightSectorShare;

        var strength = Math.Clamp(tacticStrength, 0.0, 1.0);

        switch (tactic)
        {
            case AdvancedTactic.AttackInTheMiddle:
            {
                // Hattrick research/manual: roughly 15-30% of wing attacks
                // are redirected one-for-one into the centre.
                var transfer = 0.15 + 0.15 * strength;
                var fromWings = left + right;
                var moved = fromWings * transfer;
                left -= (left / fromWings) * moved;
                right -= (right / fromWings) * moved;
                centre += moved;
                break;
            }
            case AdvancedTactic.AttackOnWings:
            {
                // Hattrick research/manual: roughly 20-40% of centre attacks
                // are redirected one-for-one to the wings.
                var transfer = 0.20 + 0.20 * strength;
                var moved = centre * transfer;
                centre -= moved;
                left += moved / 2.0;
                right += moved / 2.0;
                break;
            }
        }

        var sum = left + centre + right;
        return sum <= 0 ? (1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0) :
            (left / sum, centre / sum, right / sum);
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
