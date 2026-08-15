namespace HattrickAI.HOEngine;

public sealed class TeamRatings
{
    public double Midfield { get; set; }
    public double LeftDefence { get; set; }
    public double CentralDefence { get; set; }
    public double RightDefence { get; set; }
    public double LeftAttack { get; set; }
    public double CentralAttack { get; set; }
    public double RightAttack { get; set; }

    public TeamRatings() { }

    public TeamRatings(
        double midfield,
        double leftDefence,
        double centralDefence,
        double rightDefence,
        double leftAttack,
        double centralAttack,
        double rightAttack)
    {
        Midfield = midfield;
        LeftDefence = leftDefence;
        CentralDefence = centralDefence;
        RightDefence = rightDefence;
        LeftAttack = leftAttack;
        CentralAttack = centralAttack;
        RightAttack = rightAttack;
    }

    public double AverageDefence =>
        (LeftDefence + CentralDefence + RightDefence) / 3.0;

    public double AverageAttack =>
        (LeftAttack + CentralAttack + RightAttack) / 3.0;

    public double OverallStrength =>
        Midfield * 1.30 + AverageDefence * 1.05 + AverageAttack * 1.10;

    public string StrongestAttackSide
    {
        get
        {
            double max = Math.Max(LeftAttack, Math.Max(CentralAttack, RightAttack));
            if (Math.Abs(max - CentralAttack) < .0001) return "Merkez";
            return Math.Abs(max - LeftAttack) < .0001 ? "Sol" : "Sağ";
        }
    }

    public string WeakestDefenceSide
    {
        get
        {
            double min = Math.Min(LeftDefence, Math.Min(CentralDefence, RightDefence));
            if (Math.Abs(min - CentralDefence) < .0001) return "Merkez";
            return Math.Abs(min - LeftDefence) < .0001 ? "Sol" : "Sağ";
        }
    }

    public override string ToString() =>
        $"MF={Midfield:F2} LD={LeftDefence:F2} CD={CentralDefence:F2} RD={RightDefence:F2} LA={LeftAttack:F2} CA={CentralAttack:F2} RA={RightAttack:F2}";
}
