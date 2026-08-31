namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 6/Threat layer: converts opponent seven-sector ratings into the
/// defensive threat faced by the corresponding sectors. No player skills are
/// fabricated here; it works from the opponent rating already available from
/// CHPP matchdetails or the opponent estimator.
/// </summary>
public sealed class OpponentThreatEngine
{
    public OpponentThreatMap Analyze(RegionalRatingSnapshot opponent)
    {
        ArgumentNullException.ThrowIfNull(opponent);

        return new OpponentThreatMap(
            LeftThreat: opponent.LeftAttack,
            CenterThreat: opponent.CentralAttack,
            RightThreat: opponent.RightAttack,
            MidfieldPressure: opponent.Midfield,
            LeftDefenceBarrier: opponent.LeftDefence,
            CenterDefenceBarrier: opponent.CentralDefence,
            RightDefenceBarrier: opponent.RightDefence);
    }

    public double DefensiveNeed(double opponentAttack, double ownDefence)
        => Math.Max(0, opponentAttack - ownDefence);

    public double MatchupMargin(double ownAttack, double opponentDefence)
        => ownAttack - opponentDefence;
}

public sealed record OpponentThreatMap(
    double LeftThreat,
    double CenterThreat,
    double RightThreat,
    double MidfieldPressure,
    double LeftDefenceBarrier,
    double CenterDefenceBarrier,
    double RightDefenceBarrier)
{
    public double MaxAttackThreat => Math.Max(LeftThreat, Math.Max(CenterThreat, RightThreat));
    public double TotalAttackThreat => LeftThreat + CenterThreat + RightThreat;
}
