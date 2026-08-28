namespace HattrickAI.HOEngine;

public class TeamInput
{
    public string TeamName { get; set; } = "";

    public TeamRatings Ratings { get; set; } = new();

    public int TacticType { get; set; }

    public int TacticLevel { get; set; }

    public TeamInput()
    {
    }

    public TeamInput(
        string teamName,
        TeamRatings ratings)
    {
        TeamName = teamName;
        Ratings = ratings;
    }
}