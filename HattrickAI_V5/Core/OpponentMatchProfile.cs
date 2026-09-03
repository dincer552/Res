namespace HattrickAI.V5.Core;

/// <summary>
/// Match-time opponent snapshot. Rating/threat are always available;
/// lineup + player roster are optional so M9 can resolve opponent Specialty
/// events when CHPP data supplies them.
/// </summary>
public sealed record OpponentMatchProfile(
    string TeamName,
    string Formation,
    RegionalRatingSnapshot Rating,
    OpponentThreatMap Threat)
{
    public IReadOnlyList<Player> Players { get; init; } = [];
    public Lineup? LastMatchLineup { get; init; }

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
