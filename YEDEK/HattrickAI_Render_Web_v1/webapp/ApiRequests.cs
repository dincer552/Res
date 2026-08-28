using HattrickAI.HOEngine;

public sealed record SimulationRequest(
    TeamRatings Home,
    TeamRatings Away,
    int Simulations = 10000,
    int HomeTacticType = 0,
    int HomeTacticLevel = 0,
    int AwayTacticType = 0,
    int AwayTacticLevel = 0);

public sealed record RecommendationRequest(
    List<PlayerData> Players,
    TeamData Opponent,
    int Simulations = 10000,
    bool IsHome = true);
