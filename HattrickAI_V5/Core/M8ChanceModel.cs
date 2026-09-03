using System;
namespace HattrickAI.V5.Core;

public sealed class M8ChanceModel
{
    public M8ChanceResult Calculate(M8TacticalMatchupInput own, RegionalRatingSnapshot opponent)
    {
        ArgumentNullException.ThrowIfNull(own); ArgumentNullException.ThrowIfNull(opponent);

        var allocation = M8ChanceAllocationEngine.Calculate(
            own.OwnRating.Midfield,
            opponent.Midfield,
            own.Tactic,
            own.TacticalLevel.Value);
        var sectorShares = M8ChanceAllocationEngine.CalculateSectorShares(own.Tactic, own.TacticalLevel.Value);

        // Hattrick sector matchups: left attack vs opponent right defence,
        // centre vs centre, right attack vs opponent left defence.
        var leftAttack = ScoreProbability(own.OwnRating.LeftAttack, opponent.RightDefence);
        var centreAttack = ScoreProbability(own.OwnRating.CentralAttack, opponent.CentralDefence);
        var rightAttack = ScoreProbability(own.OwnRating.RightAttack, opponent.LeftDefence);

        var opponentLeftAttack = ScoreProbability(opponent.LeftAttack, own.OwnRating.RightDefence);
        var opponentCentreAttack = ScoreProbability(opponent.CentralAttack, own.OwnRating.CentralDefence);
        var opponentRightAttack = ScoreProbability(opponent.RightAttack, own.OwnRating.LeftDefence);

        var ownRegularQuality = WeightedRegularQuality(
            leftAttack, centreAttack, rightAttack,
            sectorShares.Left, sectorShares.Centre, sectorShares.Right);
        var opponentRegularQuality = WeightedRegularQuality(
            opponentLeftAttack, opponentCentreAttack, opponentRightAttack,
            sectorShares.Left, sectorShares.Centre, sectorShares.Right);

        var ownRegularOwnership = allocation.OwnRegularChanceExpected /
            Math.Max(1e-9, allocation.OwnRegularChanceExpected + allocation.OpponentRegularChanceExpected);
        var structuralChance = Clamp01(
            ownRegularOwnership * ownRegularQuality + sectorShares.SetPiece * 0.5);

        return new M8ChanceResult(
            own.CandidateId,
            allocation.PossessionProbability,
            leftAttack,
            centreAttack,
            rightAttack,
            sectorShares.Left,
            sectorShares.Centre,
            sectorShares.Right,
            sectorShares.SetPiece,
            structuralChance,
            own.Tactic,
            own.CalibrationStatus)
        {
            Allocation = allocation,
            OpponentLeftAttackVsOwnRightDefence = opponentLeftAttack,
            OpponentCentreAttackVsOwnCentreDefence = opponentCentreAttack,
            OpponentRightAttackVsOwnLeftDefence = opponentRightAttack
        };
    }

    private static double WeightedRegularQuality(
        double left, double centre, double right,
        double leftWeight, double centreWeight, double rightWeight)
    {
        var sum = leftWeight + centreWeight + rightWeight;
        return sum <= 0.0
            ? 0.5
            : Clamp01((left * leftWeight + centre * centreWeight + right * rightWeight) / sum);
    }

    // PDF Eq. 4: 0.92*(4A-3)^3.5 / (0.92*(4A-3)^3.5 + (4D-3)^3.5).
    private static double ScoreProbability(double attack, double defence)
    {
        var a = Math.Max(0.0, attack) * 4.0 - 3.0;
        var d = Math.Max(0.0, defence) * 4.0 - 3.0;
        a = Math.Max(0.0, a);
        d = Math.Max(0.0, d);
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
    public double OpenChancePool => Allocation.OpenChancePool;
    public double PressingSuppression => Allocation.PressingSuppression;
    public double TacticConversionRate => Allocation.TacticConversionRate;
    public double LongShotConversionRate => Allocation.LongShotConversionRate;
}
