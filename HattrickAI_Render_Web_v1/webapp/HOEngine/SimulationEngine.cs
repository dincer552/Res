namespace HattrickAI.HOEngine;

public class SimulationEngine
{
    private readonly MatchSimulator simulator = new();

    public SimulationResult Run(
        TeamRatings homeRatings,
        TeamRatings awayRatings,
        int simulationCount = 1000,
        int homeTacticType = 0,
        int homeTacticLevel = 0,
        int awayTacticType = 0,
        int awayTacticLevel = 0)
    {
        // /api/simulate passes the seven raw sector ratings straight into HO's
        // TeamData.  The simulator itself performs the seven pairwise sector
        // comparisons exactly as BaseActionGenerator.compare().
        var homeTeam = new TeamData(
            "Ev Sahibi",
            homeRatings,
            homeTacticType,
            homeTacticLevel);

        var awayTeam = new TeamData(
            "Deplasman",
            awayRatings,
            awayTacticType,
            awayTacticLevel);

        return simulator.Run(homeTeam, awayTeam, simulationCount);
    }

    public SectorComparison CompareSectors(TeamRatings homeRatings, TeamRatings awayRatings)
        => SectorComparison.From(homeRatings, awayRatings);

    public string CreateSummary(
        TeamRatings homeRatings,
        TeamRatings awayRatings,
        int simulationCount = 1000)
    {
        SimulationResult result =
            Run(homeRatings, awayRatings, simulationCount);

        return
            $"Simülasyon: {simulationCount}\n\n" +
            $"Ev sahibi kazanır: {result.HomeWinPercentage:F1}%\n" +
            $"Beraberlik: {result.DrawPercentage:F1}%\n" +
            $"Deplasman kazanır: {result.AwayWinPercentage:F1}%\n\n" +
            $"Ortalama skor: " +
            $"{result.AverageHomeGoals:F2} - " +
            $"{result.AverageAwayGoals:F2}\n\n" +
            $"En olası skor: " +
            $"{result.GetMostLikelyScore()}";
    }
}
