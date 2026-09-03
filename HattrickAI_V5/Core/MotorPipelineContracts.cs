namespace HattrickAI.V5.Core;

/// <summary>
/// Ortak veri sözleşmeleri. Motorlar birbirinin iç implementasyonuna
doğrudan bağımlı olmak yerine bu kanonik çıktılar üzerinden haberleşir.
/// </summary>
public sealed record MatchDataContext(
    IReadOnlyList<Player> OwnPlayers,
    int OwnTeamId,
    string OwnTeamName,
    OpponentMatchProfile Opponent,
    RatingContext RatingContext,
    MatchQuestionnaire Questionnaire);

public sealed record PlayerAnalysisResult(IReadOnlyList<PlayerAnalysisProfile> Players)
{
    public PlayerAnalysisProfile? Find(int playerId)
        => Players.FirstOrDefault(x => x.PlayerId == playerId);
}

public sealed record FormationCandidate(
    string Formation,
    IReadOnlyList<string> SlotCodes,
    double StructuralScore = 0);

public sealed record FormationCandidateSet(IReadOnlyList<FormationCandidate> Candidates);

public sealed record PositionAssignmentCandidate(
    string Formation,
    Lineup Lineup,
    double SuitabilityScore,
    IReadOnlyDictionary<int, string>? PlayerAssignments = null,
    double StructuralScore = 0)
{
    public string FormationId => Formation;
    public string LineupId => CandidateId;
    public string CandidateId => string.Join(";", Lineup.Slots
        .OrderBy(x => x.Code, StringComparer.Ordinal)
        .Select(x => $"{x.Code}:{x.PlayerId}"));
}

public sealed record BehaviourPlanCandidate(
    Lineup Lineup,
    double StructuralScore,
    double BehaviourScore);

public sealed record MatchupEvaluation(
    double MidfieldMargin,
    double LeftAttackMargin,
    double CentralAttackMargin,
    double RightAttackMargin,
    double LeftDefenceMargin,
    double CentralDefenceMargin,
    double RightDefenceMargin,
    double OverallScore);

public sealed record TacticalCandidate(
    Lineup Lineup,
    RegionalRatingSnapshot Rating,
    MatchupEvaluation Matchup,
    double TacticalScore);

public sealed record FinalMatchPlan(
    string Formation,
    Lineup Lineup,
    RegionalRatingSnapshot Rating,
    MatchupEvaluation Matchup,
    double TacticalScore);

public sealed record MatchPrediction(
    double PossessionProbability,
    double ExpectedHomeGoals,
    double ExpectedAwayGoals,
    double WinProbability,
    double DrawProbability,
    double LossProbability)
{
    public MatchLocation Location { get; init; } = MatchLocation.Home;
    public M9EventGoalBreakdown EventGoals { get; init; } = M9EventGoalBreakdown.Empty;
    private M9SimulationResult? _simulation;
    public M9SimulationResult Simulation => _simulation ??= M9SimulationEngine.Simulate(this);
}

public interface IPlayerAnalysisEngine
{
    PlayerAnalysisResult Analyze(IReadOnlyList<Player> players);
}

public interface IFormationCandidateEngine
{
    FormationCandidateSet Generate(MatchDataContext context, PlayerAnalysisResult players);
}

public interface IPositionOptimizationEngine
{
    IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidate formation);
}

public interface IBehaviourOptimizationEngine
{
    IReadOnlyList<BehaviourPlanCandidate> GenerateCandidates(
        MatchDataContext context,
        PositionAssignmentCandidate xi);
}
