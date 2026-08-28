namespace HattrickAI.HOEngine;

public class MatchData
{
    public string HomeTeamName { get; set; } = "";

    public string AwayTeamName { get; set; } = "";

    public TeamRatings HomeRatings { get; set; } = new();

    public TeamRatings AwayRatings { get; set; } = new();

    public int HomeGoals { get; set; }

    public int AwayGoals { get; set; }

    public DateTime MatchDate { get; set; }
}
