namespace HattrickAI.HOEngine;

public class OpponentMatchData
{
    public string MatchId { get; }

    public TeamData HomeTeam { get; }

    public TeamData AwayTeam { get; }

    public OpponentMatchData(
        string matchId,
        TeamData homeTeam,
        TeamData awayTeam)
    {
        MatchId = matchId;
        HomeTeam = homeTeam;
        AwayTeam = awayTeam;
    }
}
