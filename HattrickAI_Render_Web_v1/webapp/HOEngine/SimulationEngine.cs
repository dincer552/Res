namespace HattrickAI.HOEngine;

public class SimulationEngine
{
    private readonly MatchSimulator simulator = new();

    public SimulationResult Run(
        TeamRatings homeRatings,
        TeamRatings awayRatings,
        int simulationCount = 1000)
    {
        var homeTeam = new TeamData(
            "Ev Sahibi",
            homeRatings,
            0,
            0);

        var awayTeam = new TeamData(
            "Deplasman",
            awayRatings,
            0,
            0);

        return simulator.Run(
            homeTeam,
            awayTeam,
            simulationCount);
    }

    public string CreateSummary(
        TeamRatings homeRatings,
        TeamRatings awayRatings,
        int simulationCount = 1000)
    {
        SimulationResult result =
            Run(
                homeRatings,
                awayRatings,
                simulationCount);

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