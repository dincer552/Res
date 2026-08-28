using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.Backtest;

public sealed class WalkForwardBacktestEngine
{
    private readonly ChppMatchDataService _matches;
    private readonly ChppTeamDataService _teams;
    private readonly RecommendationEngine _engine = new();

    public WalkForwardBacktestEngine(ChppMatchDataService matches, ChppTeamDataService teams)
    { _matches = matches; _teams = teams; }

    public async Task<BacktestSummary> RunAsync(int ownTeamId, int matchCount = 30, int simulations = 10000, CancellationToken cancellationToken = default)
    {
        // NOTE: CHPP matchdetails exposes historical team ratings, not a historical player roster.
        // Therefore this first backtest uses the current roster only as an explicit limitation and
        // NEVER uses the target match or later matches for opponent ratings. The API reports this limitation.
        var currentTeam = await _teams.LoadOwnTeamAsync();
        var fixtures = (await _matches.LoadRecentCompletedFixturesAsync(ownTeamId, matchCount, cancellationToken)).OrderBy(x => x.MatchDate).ToList();
        var results = new List<BacktestMatchResult>();
        foreach (var fixture in fixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _matches.BuildHistoricalSnapshotAsync(fixture, ownTeamId, cancellationToken);
            if (snapshot.CutoffDate <= snapshot.OpponentRecentMatches.Select(x => x.Fixture.MatchDate).DefaultIfEmpty(DateTime.MinValue).Max())
                throw new InvalidOperationException($"Look-ahead bias detected for match {fixture.MatchId}.");

            var isHome = fixture.HomeTeamId == ownTeamId;
            var recommendation = _engine.Recommend(currentTeam.Players, snapshot.OpponentTeam, simulations, isHome);
            if (recommendation is null) continue;
            var sim = recommendation.Simulation;
            var predicted = isHome
                ? (sim.HomeWinPercentage >= sim.DrawPercentage && sim.HomeWinPercentage >= sim.AwayWinPercentage ? "W" : sim.AwayWinPercentage >= sim.DrawPercentage ? "L" : "D")
                : (sim.AwayWinPercentage >= sim.DrawPercentage && sim.AwayWinPercentage >= sim.HomeWinPercentage ? "W" : sim.HomeWinPercentage >= sim.DrawPercentage ? "L" : "D");
            var actual = isHome ? (fixture.HomeGoals > fixture.AwayGoals ? "W" : fixture.HomeGoals < fixture.AwayGoals ? "L" : "D") : (fixture.AwayGoals > fixture.HomeGoals ? "W" : fixture.AwayGoals < fixture.HomeGoals ? "L" : "D");
            var ourExpected = isHome ? sim.AverageHomeGoals : sim.AverageAwayGoals;
            var againstExpected = isHome ? sim.AverageAwayGoals : sim.AverageHomeGoals;
            var ourActual = isHome ? fixture.HomeGoals!.Value : fixture.AwayGoals!.Value;
            var againstActual = isHome ? fixture.AwayGoals!.Value : fixture.HomeGoals!.Value;
            results.Add(new BacktestMatchResult(fixture.MatchId, fixture.MatchDate, fixture.HomeTeamName, fixture.AwayTeamName, fixture.HomeGoals!.Value, fixture.AwayGoals!.Value, predicted, sim.HomeWinPercentage, sim.DrawPercentage, sim.AwayWinPercentage, sim.AverageHomeGoals, sim.AverageAwayGoals, recommendation.Formation, recommendation.TacticName, recommendation.Ratings, predicted == actual, Math.Abs(ourExpected - ourActual) + Math.Abs(againstExpected - againstActual)));
        }

        var n = results.Count; var correct = results.Count(x => x.ResultDirectionCorrect);
        var brier = n == 0 ? 0 : results.Average(x => {
            var h = x.ActualHomeGoals > x.ActualAwayGoals ? 1 : 0; var d = x.ActualHomeGoals == x.ActualAwayGoals ? 1 : 0; var a = x.ActualHomeGoals < x.ActualAwayGoals ? 1 : 0;
            return Math.Pow(x.HomeWinPercentage / 100 - h, 2) + Math.Pow(x.DrawPercentage / 100 - d, 2) + Math.Pow(x.AwayWinPercentage / 100 - a, 2);
        });
        return new BacktestSummary(n, correct, n == 0 ? 0 : correct * 100.0 / n, brier, n == 0 ? 0 : results.Average(x => x.AbsoluteGoalError), results, "HOEngine-WalkForward-1");
    }
}
