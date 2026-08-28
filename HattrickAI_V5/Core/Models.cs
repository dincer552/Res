namespace HattrickAI.V5.Core;

public sealed record Player(
    int Id,
    string Name,
    int Keeper,
    int Defending,
    int Playmaking,
    int Passing,
    int Winger,
    int Scoring,
    int Stamina,
    int Form,
    int Experience);

public sealed record Slot(
    string Code,
    string Label,
    string Description,
    string? PlayerName,
    int PlayerId,
    double Rating,
    double X,
    double Y);

public sealed record Lineup(
    string TeamName,
    string Formation,
    IReadOnlyList<Slot> Slots);

public sealed record MatchInfo(
    int MatchId,
    DateTimeOffset Date,
    string HomeTeam,
    int HomeTeamId,
    string AwayTeam,
    int AwayTeamId,
    int? HomeGoals,
    int? AwayGoals,
    int MatchType,
    string MatchTypeName);

public sealed record Analysis(
    string Build,
    string TeamName,
    string OpponentName,
    string MatchTitle,
    MatchInfo? OpponentReferenceMatch,
    Lineup Own,
    Lineup Opponent);
