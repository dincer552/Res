using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.Backtest;

public sealed record BacktestRequest(int MatchCount = 30, int Simulations = 10000);

public sealed record HistoricalMatchSnapshot(
    int MatchId,
    DateTime CutoffDate,
    ChppFixture Fixture,
    TeamData OwnTeam,
    TeamData OpponentTeam,
    IReadOnlyList<ChppOpponentMatch> OpponentRecentMatches);

public sealed record BacktestMatchResult(
    int MatchId,
    DateTime MatchDate,
    string HomeTeam,
    string AwayTeam,
    int ActualHomeGoals,
    int ActualAwayGoals,
    string PredictedResult,
    double HomeWinPercentage,
    double DrawPercentage,
    double AwayWinPercentage,
    double AverageHomeGoals,
    double AverageAwayGoals,
    string Formation,
    string Tactic,
    TeamRatings Ratings,
    bool ResultDirectionCorrect,
    double AbsoluteGoalError);

public sealed record BacktestSummary(
    int MatchCount,
    int CorrectResultDirections,
    double ResultDirectionAccuracy,
    double BrierScore,
    double AverageAbsoluteGoalError,
    IReadOnlyList<BacktestMatchResult> Matches,
    string EngineVersion);
