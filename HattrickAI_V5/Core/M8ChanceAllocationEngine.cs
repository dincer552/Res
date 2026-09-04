using System;

namespace HattrickAI.V5.Core;

/// <summary>
/// M8 chance-allocation layer based on the 2026 Hattrick research paper.
/// The paper-derived mechanism is the production baseline; local CHPP data is
/// validation only and never overwrites the research mechanism automatically.
/// </summary>
public static class M8ChanceAllocationEngine
{
    public const int ExclusiveChancesPerTeam = 5;
    public const int OpenChancePool = 5;
    public const double PaperLeftAttackShare = 0.2565;
    public const double PaperCentreAttackShare = 0.3615;
    public const double PaperRightAttackShare = 0.2565;
    public const double PaperDirectFreeKickShare = 0.0586;
    public const double PaperIndirectFreeKickShare = 0.0418;
    public const double PaperPenaltyKickShare = 0.0251;
    public const double PaperRegularSectorShare = PaperLeftAttackShare + PaperCentreAttackShare + PaperRightAttackShare;
    public const double PaperExpectedNormalAttacks = 10.0;
    public const double PaperExpectedRegularSectorChances = PaperExpectedNormalAttacks * PaperRegularSectorShare;

    public const double AiMMinWingConversion = 0.20;
    public const double AiMMaxWingConversion = 0.35;
    public const double AoWMinCentreConversion = 0.34;
    public const double AoWMaxCentreConversion = 0.52;
    public const double LongShotsMinConversion = 0.06;
    public const double LongShotsMaxConversion = 0.43;
    public const double PressingMinSuppression = 0.05;
    public const double PressingMaxSuppression = 0.41;
    public const double CounterAttackMinConversion = 0.04;
    public const double CounterAttackMaxConversion = 0.45;
    public const double CounterAttackMidfieldPenalty = 0.07;

    public const double CalibratedTotalRegularChances = 8.8;
    public const double CalibratedOwnershipIntercept = -0.4380926172;
    public const double CalibratedOwnershipMidfieldSlope = 1.9561688498;

    public static DiscreteChanceAllocation Calculate(double ownMidfieldRating, double opponentMidfieldRating, AdvancedTactic tactic = AdvancedTactic.Normal, double tacticStrength = 0.0)
    {
        var effectiveOwnMidfield = tactic == AdvancedTactic.CounterAttack ? Math.Max(0.0, ownMidfieldRating * (1.0 - CounterAttackMidfieldPenalty)) : ownMidfieldRating;
        var possession = CalculatePossessionProbability(effectiveOwnMidfield, opponentMidfieldRating);
        var pressingSuppression = tactic == AdvancedTactic.Pressing ? CalculateTacticConversionRate(tactic, tacticStrength) : 0.0;
        var normalVolumeFactor = 1.0 - pressingSuppression;
        var ownExpected = PaperExpectedRegularSectorChances * possession * normalVolumeFactor;
        var opponentExpected = PaperExpectedRegularSectorChances * (1.0 - possession) * normalVolumeFactor;
        var sector = CalculateSectorShares(tactic, tacticStrength);
        var appliedConversion = CalculateTacticConversionRate(tactic, tacticStrength);

        return new DiscreteChanceAllocation(ExclusiveChancesPerTeam, ExclusiveChancesPerTeam, OpenChancePool,
            OpenChancePool * possession * normalVolumeFactor, OpenChancePool * (1.0 - possession) * normalVolumeFactor,
            ownExpected, opponentExpected,
            $"PDF Eq1+Eq2+Eq3; tactic={tactic}; exact paper TCR={appliedConversion:P1}; pressing suppression={pressingSuppression:P1}.")
        {
            PossessionProbability = possession,
            EffectiveOwnMidfield = effectiveOwnMidfield,
            PressingSuppression = pressingSuppression,
            TacticConversionRate = appliedConversion,
            LongShotConversionRate = tactic == AdvancedTactic.LongShots ? appliedConversion : 0.0,
            CounterAttackConversionRate = tactic == AdvancedTactic.CounterAttack ? appliedConversion : 0.0,
            CounterAttackEligible = tactic == AdvancedTactic.CounterAttack && ownMidfieldRating < opponentMidfieldRating,
            SectorLeftShare = sector.Left,
            SectorCentreShare = sector.Centre,
            SectorRightShare = sector.Right,
            SectorSetPieceShare = sector.SetPiece
        };
    }

    public static DiscreteChanceAllocation Calculate(double ownMidfieldShare)
    {
        var possession = Math.Clamp(ownMidfieldShare, 0.0, 1.0);
        return new DiscreteChanceAllocation(ExclusiveChancesPerTeam, ExclusiveChancesPerTeam, OpenChancePool,
            OpenChancePool * possession, OpenChancePool * (1.0 - possession),
            PaperExpectedRegularSectorChances * possession, PaperExpectedRegularSectorChances * (1.0 - possession),
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
        own = Math.Max(0.0, own); opponent = Math.Max(0.0, opponent);
        var ownPower = Math.Pow(own, 3.0); var opponentPower = Math.Pow(opponent, 3.0);
        var total = ownPower + opponentPower;
        return total <= 0.0 ? 0.5 : Math.Clamp(ownPower / total, 0.0, 1.0);
    }

    /// <summary>Entry point used by V5. Its tacticStrength is the V5 0-10 internal scale.</summary>
    public static double CalculateTacticConversionRate(AdvancedTactic tactic, double tacticStrength)
        => CalculateTacticConversionRateFromPaperRt(tactic, TacticPaperMappingEngine.ToPaperRt(tacticStrength));

    /// <summary>Equation B.2 from Constantinou et al. (2026), accepting paper RT directly.</summary>
    public static double CalculateTacticConversionRateFromPaperRt(AdvancedTactic tactic, double tacticRating)
    {
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

    [Obsolete("Use CalculateTacticConversionRate for V5 scale or CalculateTacticConversionRateFromPaperRt for paper RT.")]
    public static double CalculateAppliedTacticRate(AdvancedTactic tactic, double tacticStrength)
        => CalculateTacticConversionRate(tactic, tacticStrength);

    public static (double Left, double Centre, double Right, double SetPiece) CalculateSectorShares(AdvancedTactic tactic = AdvancedTactic.Normal, double tacticStrength = 0.0)
    {
        var left = PaperLeftAttackShare; var centre = PaperCentreAttackShare; var right = PaperRightAttackShare;
        var setPiece = PaperDirectFreeKickShare + PaperIndirectFreeKickShare + PaperPenaltyKickShare;
        var strength = Math.Max(0.0, tacticStrength);
        switch (tactic)
        {
            case AdvancedTactic.AttackMiddle:
                var movedMiddle = (left + right) * CalculateTacticConversionRate(tactic, strength);
                left -= (left / (left + right)) * movedMiddle; right -= (right / (left + right)) * movedMiddle; centre += movedMiddle; break;
            case AdvancedTactic.AttackWings:
                var movedWings = centre * CalculateTacticConversionRate(tactic, strength);
                centre -= movedWings; left += movedWings / 2.0; right += movedWings / 2.0; break;
        }
        var sum = left + centre + right + setPiece;
        return sum <= 0.0 ? (PaperLeftAttackShare, PaperCentreAttackShare, PaperRightAttackShare, 0.1255) : (left / sum, centre / sum, right / sum, setPiece / sum);
    }
}

public sealed record DiscreteChanceAllocation(int OwnExclusive, int OpponentExclusive, int OpenChancePool, double OwnOpenExpected, double OpponentOpenExpected, double OwnRegularChanceExpected, double OpponentRegularChanceExpected, string Mechanism)
{
    public double PossessionProbability { get; init; }
    public double EffectiveOwnMidfield { get; init; }
    public double PressingSuppression { get; init; }
    public double TacticConversionRate { get; init; }
    public double LongShotConversionRate { get; init; }
    public double CounterAttackConversionRate { get; init; }
    public bool CounterAttackEligible { get; init; }
    public double SectorLeftShare { get; init; }
    public double SectorCentreShare { get; init; }
    public double SectorRightShare { get; init; }
    public double SectorSetPieceShare { get; init; }
}