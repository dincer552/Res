using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer.
/// Phase D combines the 2026 Hattrick research paper with the 60-match CHPP
/// calibration set. The paper's discrete exclusive/shared mechanism is kept;
/// the local dataset is used as validation rather than replacing the mechanism.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;

    // Research paper Eq. 3: L/M/R probabilities. These are probabilities over
    // all normal attacks, including set-pieces.
    public const double PaperLeftAttackShare = 0.2565;
    public const double PaperCentreAttackShare = 0.3615;
    public const double PaperRightAttackShare = 0.2565;
    public const double PaperDirectFreeKickShare = 0.0586;
    public const double PaperIndirectFreeKickShare = 0.0418;
    public const double PaperPenaltyKickShare = 0.0251;

    public const double PaperRegularSectorShare =
        PaperLeftAttackShare + PaperCentreAttackShare + PaperRightAttackShare;

    // 10 expected normal attacks from Eq. 2, multiplied by the L/M/R share.
    // The 60-match CHPP set observed 8.8 L/M/R chances on average, validating
    // this paper-derived 8.745 expectation (difference = 0.055).
    public const double PaperExpectedNormalAttacks = 10.0;
    public const double PaperExpectedRegularSectorChances =
        PaperExpectedNormalAttacks * PaperRegularSectorShare;

    // Compatibility/diagnostic constants retained from the prior calibration.
    public const double CalibratedTotalRegularChances = 8.8;
    public const double CalibratedOwnershipIntercept = -0.4380926172;
    public const double CalibratedOwnershipMidfieldSlope = 1.9561688498;

    public static DiscreteChanceAllocation Calculate(double ownMidfieldRating, double opponentMidfieldRating)
    {
        var possession = CalculatePossessionProbability(ownMidfieldRating, opponentMidfieldRating);
        var ownExpected = PaperExpectedRegularSectorChances * possession;
        var opponentExpected = PaperExpectedRegularSectorChances * (1.0 - possession);

        return CreateAllocation(
            possession,
            ownExpected,
            opponentExpected,
            "PDF Eq1+Eq2: POS=(4M-3)^3/((4Mown-3)^3+(4Mopp-3)^3); 5 exclusive + 5 shared; LMR expected=8.745.");
    }

    /// <summary>
    /// Backward-compatible overload. The supplied value is already interpreted
    /// as the possession probability, so no synthetic midfield rating is made.
    /// </summary>
    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        var possession = Math.Clamp(ownMidfieldShare, 0.0, 1.0);
        var ownExpected = PaperExpectedRegularSectorChances * possession;
        var opponentExpected = PaperExpectedRegularSectorChances * (1.0 - possession);
        return CreateAllocation(
            possession,
            ownExpected,
            opponentExpected,
            "PDF Eq1+Eq2: supplied midfield-share interpreted as POS; LMR expected=8.745.");
    }

    public static double CalculatePossessionProbability(double ownMidfieldRating, double opponentMidfieldRating)
    {
        var own = Math.Max(0.0, ownMidfieldRating) * 4.0 - 3.0;
        var opponent = Math.Max(0.0, opponentMidfieldRating) * 4.0 - 3.0;
        own = Math.Max(0.0, own);
        opponent = Math.Max(0.0, opponent);
        var ownPower = Math.Pow(own, 3.0);
        var opponentPower = Math.Pow(opponent, 3.0);
        var total = ownPower + opponentPower;
        return total <= 0.0 ? 0.5 : Math.Clamp(ownPower / total, 0.0, 1.0);
    }

    public static (double Left, double Centre, double Right, double SetPiece) CalculateSectorShares(
        AdvancedTactic tactic = AdvancedTactic.Normal,
        double tacticStrength = 0.0)
    {
        var left = PaperLeftAttackShare;
        var centre = PaperCentreAttackShare;
        var right = PaperRightAttackShare;
        var setPiece = PaperDirectFreeKickShare + PaperIndirectFreeKickShare + PaperPenaltyKickShare;
        var strength = Math.Clamp(tacticStrength, 0.0, 10.0);

        switch (tactic)
        {
            case AdvancedTactic.AttackMiddle:
            {
                // Paper: AiM exchanges 20-35% of wing attacks into middle.
                var transfer = 0.20 + (0.15 * strength / 10.0);
                var moved = (left + right) * transfer;
                left -= (left / (left + right)) * moved;
                right -= (right / (left + right)) * moved;
                centre += moved;
                break;
            }
            case AdvancedTactic.AttackWings:
            {
                // Paper: AoW exchanges 34-52% of middle attacks into wings.
                var transfer = 0.34 + (0.18 * strength / 10.0);
                var moved = centre * transfer;
                centre -= moved;
                left += moved / 2.0;
                right += moved / 2.0;
                break;
            }
        }

        var sum = left + centre + right + setPiece;
        return sum <= 0.0
            ? (0.2565, 0.3615, 0.2565, 0.1255)
            : (left / sum, centre / sum, right / sum, setPiece / sum);
    }

    private static DiscreteChanceAllocation CreateAllocation(
        double possession,
        double ownExpected,
        double opponentExpected,
        string mechanism)
    {
        var ownOpen = OpenChancePool * possession;
        var opponentOpen = OpenChancePool - ownOpen;
        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            ExclusiveChancesPerTeam,
            OpenChancePool,
            ownOpen,
            opponentOpen,
            ownExpected,
            opponentExpected,
            mechanism)
        {
            PossessionProbability = possession
        };
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
    string Mechanism)
{
    public double PossessionProbability { get; init; }
}
