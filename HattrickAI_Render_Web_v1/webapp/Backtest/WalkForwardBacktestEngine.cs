using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.Backtest;

public sealed class WalkForwardBacktestEngine
{
    private readonly ChppMatchDataService _matches;
    private readonly RecommendationEngine _engine = new();

    public WalkForwardBacktestEngine(ChppMatchDataService matches) => _matches = matches;

    public async Task<BacktestSummary> RunAsync(int ownTeamId, int matchCount = 30, int simulations = 10000, CancellationToken cancellationToken = default)
    {
        var fixtures = (await _matches.LoadRecentCompletedFixturesAsync(ownTeamId, matchCount, cancellationToken))
            .OrderBy(x => x.MatchDate).ToList();
        if (fixtures.Count == 0) throw new InvalidOperationException("CHPP geçmişinde tamamlanmış resmi maç bulunamadı.");

        var results = new List<BacktestMatchResult>(fixtures.Count);
        foreach (var fixture in fixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Strict cutoff: everything used by the prediction must be dated before this match.
            var snapshot = await _matches.BuildHistoricalSnapshotAsync(fixture, ownTeamId, cancellationToken);
            if (snapshot.CutoffDate <= snapshot.OpponentRecentMatches.Select(x => x.Fixture.MatchDate).DefaultIfEmpty(DateTime.MinValue).Max())
                throw new InvalidOperationException($"Look-ahead bias detected for match {fixture.MatchId}.");

            var isHome = fixture.HomeTeamId == ownTeamId;
            var recommendation = _engine.Recommend(snapshot.OwnTeam.Players, snapshot.OpponentTeam, simulations, isHome);
            if (recommendation is null) continue;

            var sim = recommendation.Simulation;
            var predicted = isHome
                ? (sim.HomeWinPercentage >= sim.DrawPercentage && sim.HomeWinPercentage >= sim.AwayWinPercentage ? "W" : sim.AwayWinPercentage >= sim.DrawPercentage ? "L" : "D")
                : (sim.AwayWinPercentage >= sim.DrawPercentage && sim.AwayWinPercentage >= sim.HomeWinPercentage ? "W" : sim.HomeWinPercentage >= sim.DrawPercentage ? "L" : "D");
            var actual = isHome
                ? (fixture.HomeGoals > fixture.AwayGoals ? "W" : fixture.HomeGoals < fixture.AwayGoals ? "L" : "D")
                : (fixture.AwayGoals > fixture.HomeGoals ? "W" : fixture.AwayGoals < fixture.HomeGoals ? "L" : "D");

            var expectedForOurTeam = isHome ? sim.AverageHomeGoals : sim.AverageAwayGoals;
            var actualForOurTeam = isHome ? fixture.HomeGoals!.Value : fixture.AwayGoals!.Value;
            var expectedAgainst = isHome ? sim.AverageAwayGoals : sim.AverageHomeGoals;
            var actualAgainst = isHome ? fixture.AwayGoals!.Value : fixture.HomeGoals!.Value;

            results.Add(new BacktestMatchResult(
                fixture.MatchId, fixture.MatchDate, fixture.HomeTeamName, fixture.AwayTeamName,
                fixture.HomeGoals!.Value, fixture.AwayGoals!.Value, predicted,
                sim.HomeWinPercentage, sim.DrawPercentage, sim.AwayWinPercentage,
                sim.AverageHomeGoals, sim.AverageAwayGoals,
                recommendation.Formation, recommendation.TacticName, recommendation.Ratings,
                predicted == actual, Math.Abs(expectedForOurTeam - actualForOurTeam) + Math.Abs(expectedAgainst - actualAgainst)));
        }

        var n = results.Count;
        var correct = results.Count(x => x.ResultDirectionCorrect);
        var brier = results.Count == 0 ? 0 : results.Average(x =>
        {
            var actualHome = x.ActualHomeGoals > x.ActualAwayGoals ? 1 : 0;
            var actualDraw = x.ActualHomeGoals == x.ActualAwayGoals ? 1 : 0;
            var actualAway = x.ActualHomeGoals < x.ActualAwayGoals ? 1 : 0;
            var hp = x.HomeWinPercentage / 100.0; var dp = x.DrawPercentage / 100.0; var ap = x.AwayWinPercentage / 100.0;
            return Math.Pow(hp - actualHome, 2) + Math.Pow(dp - actualDraw, 2) + Math.Pow(ap - actualAway, 2);
        });
        return new BacktestSummary(n, correct, n == 0 ? 0 : correct * 100.0 / n, brier,
            n == 0 ? 0 : results.Average(x => x.AbsoluteGoalError), results, "HOEngine-WalkForward-1");
    }
}
