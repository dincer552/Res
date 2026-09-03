using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer based on the 2026 Hattrick research paper.
/// The paper-derived mechanism is kept as the production baseline; the local
/// CHPP calibration set remains validation data and does not overwrite it.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;

    // Paper Eq. 3: distribution over all normal attacks.
    public const double PaperLeftAttackShare = 0.2565;
    public const double PaperCentreAttackShare = 0.3615;
    public const double PaperRightAttackShare = 0.2565;
    public const double PaperDirectFreeKickShare = 0.0586;
    public const double PaperIndirectFreeKickShare = 0.0418;
    public const double PaperPenaltyKickShare = 0.0251;
    public const double PaperRegularSectorShare = PaperLeftAttackShare + PaperCentreAttackShare + PaperRightAttackShare;

    // Paper Eq. 2: 10 expected normal attacks per match before sector allocation.
    public const double PaperExpectedNormalAttacks = 10.0;
    public const double PaperExpectedRegularSectorChances = PaperExpectedNormalAttacks * PaperRegularSectorShare;

    // Research ranges used where the paper gives a range but not a production-safe
    // closed form in the main KB-probabilistic model.
    public const double AiMMinWingConversion = 0.20;
    public const double AiMMaxWingConversion = 0.35;
    public const double AoWMinCentreConversion = 0.34;
    public const double AoWMaxCentreConversion = 0.52;
    public const double LongShotsMinConversion = 0.06;
    public const double LongShotsMaxConversion = 0.43;
    public const double PressingMinSuppression = 0.05;
    public const double PressingMaxSuppression = 0.41;
    public const double CounterAttackMidfieldPenalty = 0.07;

    // Compatibility/diagnostic constants retained from the prior calibration.
    public const double CalibratedTotalRegularChances = 8.8;
    public const double CalibratedOwnershipIntercept = -0.4380926172;
    public const double CalibratedOwnershipMidfieldSlope = 1.9561688498;

    public static DiscreteChanceAllocation Calculate(
        double ownMidfieldRating,
        double opponentMidfieldRating,
        AdvancedTactic tactic = AdvancedTactic.Normal,
        double tacticStrength = 0.0)
    {
        // Paper: Counterattack trades 7% midfield for counterattack opportunities.
        var effectiveOwnMidfield = tactic == AdvancedTactic.CounterAttack
            ? Math.Max(0.0, ownMidfieldRating * (1.0 - CounterAttackMidfieldPenalty))
            : ownMidfieldRating;

        var possession = CalculatePossessionProbability(effectiveOwnMidfield, opponentMidfieldRating);
        var totalRegular = PaperExpectedRegularSectorChances;

        // Paper: Pressing suppresses normal attacks. The published model gives a
        // 5%-41% range by tactic level; use only the source-backed range here.
        var pressingSuppression = tactic == AdvancedTactic.Pressing
            ? CalculateRange(PressingMinSuppression, PressingMaxSuppression, tacticStrength)
            : 0.0;

        var ownExpected = totalRegular * possession;
        var opponentExpected = totalRegular * (1.0 - possession) * (1.0 - pressingSuppression);

        // Re-normalise the ownership volume after a pressing suppression so the
        // suppressed chances are genuinely removed rather than redistributed.
        var sector = CalculateSectorShares(tactic, tacticStrength);
        var ownOpen = OpenChancePool * possession;
        var opponentOpen = OpenChancePool * (1.0 - possession) * (1.0 - pressingSuppression);

        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            ExclusiveChancesPerTeam,
            OpenChancePool,
            ownOpen,
            opponentOpen,
            ownExpected,
            opponentExpected,
            $"PDF Eq1+Eq2+Eq3: possession/10 normal attacks/LMR-SP distribution; tactic={tactic}; pressing suppression={pressingSuppression:P1}.")
        {
            PossessionProbability = possession,
            EffectiveOwnMidfield = effectiveOwnMidfield,
            PressingSuppression = pressingSuppression,
            TacticConversionRate = CalculateTacticConversionRate(tactic, tacticStrength),
            LongShotConversionRate = tactic == AdvancedTactic.LongShots
                ? CalculateRange(LongShotsMinConversion, LongShotsMaxConversion, tacticStrength)
                : 0.0,
            SectorLeftShare = sector.Left,
            SectorCentreShare = sector.Centre,
            SectorRightShare = sector.Right,
            SectorSetPieceShare = sector.SetPiece
        };
    }

    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        var possession = Math.Clamp(ownMidfieldShare, 0.0, 1.0);
        var ownExpected = PaperExpectedRegularSectorChances * possession;
        var opponentExpected = PaperExpectedRegularSectorChances * (1.0 - possession);
        return new DiscreteChanceAllocation(
            ExclusiveChancesPerTeam,
            ExclusiveChancesPerTeam,
            OpenChancePool,
            OpenChancePool * possession,
            OpenChancePool * (1.0 - possession),
            ownExpected,
            opponentExpected,
            "PDF Eq1+Eq2: supplied midfield-share interpreted as POS; LMR expected=8.745.")
        {
            PossessionProbability = possession,
            SectorLeftShare = PaperLeftAttackShare,
            SectorCentreShare = PaperCentreAttackShare,
            SectorRightShare = PaperRightAttackShare,
            SectorSetPieceShare = PaperDirectFreeKickShare + PaperIndirectFreeKickShare + PaperPenaltyKickShare
        };
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

    public static double CalculateTacticConversionRate(AdvancedTactic tactic, double tacticRating)
    {
        // Appendix C Eq. 2. This is retained as a research coefficient helper;
        // the main sector model uses the directly stated KB-probabilistic ranges
        // for AiM/AoW/LS/Pressing because the current V5 tactic level is not the
        // paper's raw RT scale.
        var rt = Math.Max(0.0, tacticRating);
        var raw = tactic switch
        {
            AdvancedTactic.CounterAttack => -0.617941717072569 + 0.104274398 * rt - 0.00358354796 * rt * rt + 0.0000434356 * rt * rt * rt,
            AdvancedTactic.AttackMiddle => -0.00036765 * rt * rt + 0.02180462 * rt + 0.0705084,
            AdvancedTactic.AttackWings => -0.00046569 * rt * rt + 0.02894608 * rt + 0.10514706,
            AdvancedTactic.LongShots => 0.00761935 * rt + 0.07520052,
            AdvancedTactic.Pressing => -0.00780421 * rt * rt + 0.471402 * rt - 1.10735,
            _ => 0.0
        };
        return Math.Clamp(raw, 0.0, 1.0);
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
                var transfer = CalculateRange(AiMMinWingConversion, AiMMaxWingConversion, strength);
                var moved = (left + right) * transfer;
                left -= (left / (left + right)) * moved;
                right -= (right / (left + right)) * moved;
                centre += moved;
                break;
            }
            case AdvancedTactic.AttackWings:
            {
                var transfer = CalculateRange(AoWMinCentreConversion, AoWMaxCentreConversion, strength);
                var moved = centre * transfer;
                centre -= moved;
                left += moved / 2.0;
                right += moved / 2.0;
                break;
            }
            case AdvancedTactic.LongShots:
            {
                // LS converts LMR attacks into long shots. We expose the rate
                // separately; LMR shares remain intact here so M9 can account for
                // the converted volume without silently treating it as a sector goal.
                break;
            }
        }

        var sum = left + centre + right + setPiece;
        return sum <= 0.0
            ? (PaperLeftAttackShare, PaperCentreAttackShare, PaperRightAttackShare, 0.1255)
            : (left / sum, centre / sum, right / sum, setPiece / sum);
    }

    private static double CalculateRange(double min, double max, double strength)
        => min + (max - min) * (Math.Clamp(strength, 0.0, 10.0) / 10.0);
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
    public double EffectiveOwnMidfield { get; init; }
    public double PressingSuppression { get; init; }
    public double TacticConversionRate { get; init; }
    public double LongShotConversionRate { get; init; }
    public double SectorLeftShare { get; init; }
    public double SectorCentreShare { get; init; }
    public double SectorRightShare { get; init; }
    public double SectorSetPieceShare { get; init; }
}
