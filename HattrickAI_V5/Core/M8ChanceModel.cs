using System;
namespace HattrickAI.V5.Core;

public sealed class M8ChanceModel
{
    public M8ChanceResult Calculate(M8TacticalMatchupInput own, RegionalRatingSnapshot opponent)
    {
        ArgumentNullException.ThrowIfNull(own); ArgumentNullException.ThrowIfNull(opponent);
        var midfieldShare = Share(own.OwnRating.Midfield, opponent.Midfield);
        var leftAttack = Share(own.OwnRating.LeftAttack, opponent.RightDefence);
        var centreAttack = Share(own.OwnRating.CentralAttack, opponent.CentralDefence);
        var rightAttack = Share(own.OwnRating.RightAttack, opponent.LeftDefence);
        var index = Clamp01(midfieldShare * (own.ChanceDistribution.LeftShare * leftAttack + own.ChanceDistribution.CentreShare * centreAttack + own.ChanceDistribution.RightShare * rightAttack) + own.ChanceDistribution.SetPieceShare * 0.5);
        return new M8ChanceResult(own.CandidateId, midfieldShare, leftAttack, centreAttack, rightAttack,
            own.ChanceDistribution.LeftShare, own.ChanceDistribution.CentreShare, own.ChanceDistribution.RightShare,
            own.ChanceDistribution.SetPieceShare, index, own.Tactic, CalibrationStatus.ResearchBackedStructureNeedsMatchCalibration);
    }
    private static double Share(double own, double defence)
    {
        var total = Math.Max(0, own) + Math.Max(0, defence);
        return total <= 0 ? 0.5 : Clamp01(Math.Max(0, own) / total);
    }
    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed record M8ChanceResult(
    string CandidateId, double MidfieldShare, double LeftAttackVsRightDefence,
    double CentreAttackVsCentreDefence, double RightAttackVsLeftDefence,
    double LeftChanceShare, double CentreChanceShare, double RightChanceShare,
    double SetPieceChanceShare, double StructuralChanceIndex, AdvancedTactic Tactic,
    CalibrationStatus CalibrationStatus);
