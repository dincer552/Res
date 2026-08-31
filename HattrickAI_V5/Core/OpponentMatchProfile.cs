namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2 input: opponent information that is known before choosing our XI.
/// This snapshot is intentionally independent from our own lineup so the
/// optimizer does not create a circular dependency with Motor 7.
/// </summary>
public sealed record OpponentMatchProfile(
    string TeamName,
    string Formation,
    RegionalRatingSnapshot Rating,
    OpponentThreatMap Threat)
{
    public double LeftAttack => Rating.LeftAttack;
    public double CentralAttack => Rating.CentralAttack;
    public double RightAttack => Rating.RightAttack;
    public double LeftDefence => Rating.LeftDefence;
    public double CentralDefence => Rating.CentralDefence;
    public double RightDefence => Rating.RightDefence;
    public double Midfield => Rating.Midfield;

    public double LeftAttackThreat => Threat.LeftThreat;
    public double CenterAttackThreat => Threat.CenterThreat;
    public double RightAttackThreat => Threat.RightThreat;
}
