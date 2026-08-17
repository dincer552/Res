namespace HattrickAI.HOEngine;

public class TeamData
{
    public string TeamName { get; set; }

    public TeamRatings Ratings { get; set; }

    public int TacticType { get; set; }

    public int TacticLevel { get; set; }

    // Optional UI-selected formation. When set, RecommendationEngine evaluates
    // the requested formation instead of searching all formations.
    public string? PreferredFormation { get; set; }

    public TeamData(
        string teamName,
        TeamRatings ratings,
        int tacticType,
        int tacticLevel)
    {
        TeamName = teamName;
        Ratings = ratings;
        TacticType = tacticType;
        TacticLevel = tacticLevel;
    }

    public override string ToString()
    {
        return
            $"Team={TeamName}, " +
            $"Tactic={TacticType}, " +
            $"Level={TacticLevel}, " +
            $"Ratings=[{Ratings}]";
    }
}