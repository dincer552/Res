namespace HattrickAI.HOEngine;

/// <summary>
/// The seven sector comparisons used by HO BaseActionGenerator.compare().
/// Values are probabilities in [0,1], not aggregate DEF/ATT averages.
/// </summary>
public sealed record SectorComparison(
    double Midfield,
    double RightAttackVsLeftDefence,
    double CentralAttackVsCentralDefence,
    double LeftAttackVsRightDefence,
    double RightDefenceVsLeftAttack,
    double CentralDefenceVsCentralAttack,
    double LeftDefenceVsRightAttack)
{
    public static SectorComparison From(TeamRatings team, TeamRatings opponent)
    {
        return new SectorComparison(
            Linear(team.Midfield, opponent.Midfield),
            Linear(team.RightAttack, opponent.LeftDefence),
            Linear(team.CentralAttack, opponent.CentralDefence),
            Linear(team.LeftAttack, opponent.RightDefence),
            Linear(team.RightDefence, opponent.LeftAttack),
            Linear(team.CentralDefence, opponent.CentralAttack),
            Linear(team.LeftDefence, opponent.RightAttack));
    }

    private static double Linear(double first, double second)
    {
        double total = first + second;
        return total <= 0 ? 0.5 : first / total;
    }
}
