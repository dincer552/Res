using System;
namespace HattrickAI.V5.Core;

/// <summary>
/// M8: M7 rating + M7.2 PDF tactical scenario -> chance allocation -> sector scoring probability.
/// M7.2 owns tactical distribution and special-event volume context; M8 resolves chance ownership,
/// sector matchups and tactic-specific opportunity volumes.
/// </summary>
public sealed class M8ChanceModel
{
    public M8ChanceResult Calculate(M8TacticalMatchupInput own, RegionalRatingSnapshot opponent)
    {
        ArgumentNullException.ThrowIfNull(own); ArgumentNullException.ThrowIfNull(opponent);

        var allocation = M8ChanceAllocationEngine.Calculate(own.OwnRating.Midfield, opponent.Midfield, own.Tactic, own.TacticalLevel.Value);
        var distribution = own.ChanceDistribution;
        allocation = allocation with
        {
            TacticConversionRate = own.Tactic switch
            {
                AdvancedTactic.AttackMiddle => distribution.AiMWingToCentreRate,
                AdvancedTactic.AttackWings => distribution.AoWCentreToWingRate,
                AdvancedTactic.LongShots => distribution.LongShotConversionRate,
                AdvancedTactic.Pressing => distribution.PressingSuppressionRate,
                AdvancedTactic.CounterAttack => distribution.CounterAttackConversionRate,
                _ => 0.0
            },
            LongShotConversionRate = distribution.LongShotConversionRate,
            CounterAttackConversionRate = distribution.CounterAttackConversionRate,
            CounterAttackEligible = own.Tactic == AdvancedTactic.CounterAttack && own.OwnRating.Midfield < opponent.Midfield,
            SectorLeftShare = distribution.LeftShare,
            SectorCentreShare = distribution.CentreShare,
            SectorRightShare = distribution.RightShare,
            SectorSetPieceShare = distribution.SetPieceShare,
            PressingSuppression = distribution.PressingSuppressionRate
        };

        var leftAttack = ScoreProbability(own.OwnRating.LeftAttack, opponent.RightDefence);
        var centreAttack = ScoreProbability(own.OwnRating.CentralAttack, opponent.CentralDefence);
        var rightAttack = ScoreProbability(own.OwnRating.RightAttack, opponent.LeftDefence);
        var opponentLeftAttack = ScoreProbability(opponent.LeftAttack, own.OwnRating.RightDefence);
        var opponentCentreAttack = ScoreProbability(opponent.CentralAttack, own.OwnRating.CentralDefence);
        var opponentRightAttack = ScoreProbability(opponent.RightAttack, own.OwnRating.LeftDefence);

        var ownRegularQuality = WeightedRegularQuality(leftAttack, centreAttack, rightAttack, distribution.LeftShare, distribution.CentreShare, distribution.RightShare);
        var opponentRegularQuality = WeightedRegularQuality(opponentLeftAttack, opponentCentreAttack, opponentRightAttack, distribution.LeftShare, distribution.CentreShare, distribution.RightShare);
        var totalRegular = Math.Max(1e-9, allocation.OwnRegularChanceExpected + allocation.OpponentRegularChanceExpected);
        var ownRegularOwnership = allocation.OwnRegularChanceExpected / totalRegular;
        var structuralChance = Clamp01(ownRegularOwnership * ownRegularQuality + distribution.SetPieceShare * 0.5);

        // PDF §4.3: CA converts an opponent's missed Normal chance into a CA opportunity,
        // only when CA starts with lower midfield before the 7% midfield penalty.
        var missedOpponentNormal = allocation.OpponentRegularChanceExpected * (1.0 - opponentRegularQuality);
        var counterAttackExpected = own.Tactic == AdvancedTactic.CounterAttack && allocation.CounterAttackEligible
            ? missedOpponentNormal * allocation.CounterAttackConversionRate
            : 0.0;

        // PDF §4.3: LS exchanges a percentage of LMR attacks for long-shot attacks.
        // The scoring probability is resolved later in M9 because the paper provides a plotted
        // relationship rather than a published closed-form equation.
        var longShotExpected = own.Tactic == AdvancedTactic.LongShots
            ? allocation.OwnRegularChanceExpected * allocation.LongShotConversionRate
            : 0.0;
        var normalRegularExpectedAfterLongShots = Math.Max(0.0, allocation.OwnRegularChanceExpected - longShotExpected);

        return new M8ChanceResult(
            own.CandidateId,
            allocation.PossessionProbability,
            leftAttack,
            centreAttack,
            rightAttack,
            distribution.LeftShare,
            distribution.CentreShare,
            distribution.RightShare,
            distribution.SetPieceShare,
            structuralChance,
            own.Tactic,
            own.CalibrationStatus)
        {
            Allocation = allocation,
            OpponentLeftAttackVsOwnRightDefence = opponentLeftAttack,
            OpponentCentreAttackVsOwnCentreDefence = opponentCentreAttack,
            OpponentRightAttackVsOwnLeftDefence = opponentRightAttack,
            OwnRegularQuality = ownRegularQuality,
            OpponentRegularQuality = opponentRegularQuality,
            MissedOpponentNormalChanceExpected = missedOpponentNormal,
            CounterAttackChanceExpected = counterAttackExpected,
            LongShotChanceExpected = longShotExpected,
            NormalRegularChanceExpectedAfterLongShots = normalRegularExpectedAfterLongShots,
            CreativeEventMultiplier = distribution.CreativeEventMultiplier
        };
    }

    private static double WeightedRegularQuality(double left, double centre, double right, double leftWeight, double centreWeight, double rightWeight)
    {
        var sum = leftWeight + centreWeight + rightWeight;
        return sum <= 0.0 ? 0.5 : Clamp01((left * leftWeight + centre * centreWeight + right * rightWeight) / sum);
    }

    // PDF Eq. 4.
    private static double ScoreProbability(double attack, double defence)
    {
        var a = Math.Max(0.0, attack) * 4.0 - 3.0;
        var d = Math.Max(0.0, defence) * 4.0 - 3.0;
        a = Math.Max(0.0, a); d = Math.Max(0.0, d);
        if (a <= 0.0 && d <= 0.0) return 0.5;
        var attackPower = 0.92 * Math.Pow(a, 3.5);
        var defencePower = Math.Pow(d, 3.5);
        var total = attackPower + defencePower;
        return total <= 0.0 ? 0.5 : Clamp01(attackPower / total);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed record M8ChanceResult(
    string CandidateId, double MidfieldShare, double LeftAttackVsRightDefence,
    double CentreAttackVsCentreDefence, double RightAttackVsLeftDefence,
    double LeftChanceShare, double CentreChanceShare, double RightChanceShare,
    double SetPieceChanceShare, double StructuralChanceIndex, AdvancedTactic Tactic,
    CalibrationStatus CalibrationStatus)
{
    public DiscreteChanceAllocation Allocation { get; init; } = M8ChanceAllocationEngine.Calculate(MidfieldShare);
    public double OpponentLeftAttackVsOwnRightDefence { get; init; }
    public double OpponentCentreAttackVsOwnCentreDefence { get; init; }
    public double OpponentRightAttackVsOwnLeftDefence { get; init; }
    public double OwnRegularChanceExpected => Allocation.OwnRegularChanceExpected;
    public double OpponentRegularChanceExpected => Allocation.OpponentRegularChanceExpected;
    public int OpenChancePool => Allocation.OpenChancePool;
    public double PressingSuppression => Allocation.PressingSuppression;
    public double TacticConversionRate => Allocation.TacticConversionRate;
    public double LongShotConversionRate => Allocation.LongShotConversionRate;
    public double CounterAttackConversionRate => Allocation.CounterAttackConversionRate;
    public bool CounterAttackEligible => Allocation.CounterAttackEligible;
    public double OwnRegularQuality { get; init; }
    public double OpponentRegularQuality { get; init; }
    public double MissedOpponentNormalChanceExpected { get; init; }
    public double CounterAttackChanceExpected { get; init; }
    public double LongShotChanceExpected { get; init; }
    public double NormalRegularChanceExpectedAfterLongShots { get; init; }
    public double CreativeEventMultiplier { get; init; } = 1.0;
}
