namespace HattrickAI.V5.Core;

/// <summary>
/// Rakip Tehdit Motoru: rakibin gerçek yedi bölgesel ratingini,
/// savunma tehdidi ve hücum fırsatı olarak karşı sektörlere eşler.
/// Oyuncu RP'si veya tahmini skill üretmez.
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

    // Opponent left attack -> our right defence.
    public double ThreatToOurLeftDefence => RightThreat;
    public double ThreatToOurCentreDefence => CenterThreat;
    public double ThreatToOurRightDefence => LeftThreat;

    // Our attack -> corresponding opposite opponent defence.
    public double OpportunityForOurLeftAttack => RightDefenceBarrier;
    public double OpportunityForOurCentreAttack => CenterDefenceBarrier;
    public double OpportunityForOurRightAttack => LeftDefenceBarrier;
}
