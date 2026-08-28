namespace HattrickAI.HOEngine;

public sealed class RecommendationResult
{
    public string Formation { get; init; } = "4-4-2";
    public string TacticName { get; init; } = "Dengeli oyun";
    public int TacticType { get; init; }
    public int TacticLevel { get; init; }
    public List<PlayerData> Lineup { get; init; } = new();
    public TeamRatings Ratings { get; init; } = new();
    public SimulationResult Simulation { get; init; } = new();
    public double SelectionScore { get; init; }
    public string Explanation { get; init; } = "";
    public IReadOnlyDictionary<int, PlayerBehaviour> BehaviourProfile { get; init; } =
        new Dictionary<int, PlayerBehaviour>();
    public int TrainingFit { get; init; }
    public int FormationExperience { get; init; }
    public string TrainingName { get; init; } = "";
    public string TrainingPriority { get; init; } = "";
}
