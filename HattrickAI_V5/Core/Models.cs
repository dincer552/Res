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

public sealed record Analysis(
    string Build,
    string TeamName,
    string OpponentName,
    string MatchTitle,
    Lineup Own,
    Lineup Opponent,
    RegionalRatingSnapshot OwnRating,
    RegionalRatingSnapshot OpponentRating)
{
    public Lineup OwnLineup => Own;
    public Lineup OpponentLineup => Opponent;
    public string OwnFormation => Own.Formation;
    public string OpponentFormation => Opponent.Formation;
}
