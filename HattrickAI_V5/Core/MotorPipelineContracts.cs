namespace HattrickAI.V5.Core;

/// <summary>
/// Ortak veri sözleşmeleri. Motorlar birbirinin iç implementasyonuna
/// doğrudan bağımlı olmak yerine bu kanonik çıktılar üzerinden haberleşir.
/// </summary>
public sealed record MatchDataContext(
    IReadOnlyList<Player> OwnPlayers,
    int OwnTeamId,
    string OwnTeamName,
    OpponentMatchProfile Opponent,
    RatingContext RatingContext,
    MatchQuestionnaire Questionnaire);

public sealed record PlayerAnalysisProfile(
    int PlayerId,
    string PlayerName,
    IReadOnlyDictionary<string, double> PositionScores,
    string PrimaryPosition,
    string SecondaryPosition,
    double PrimaryScore,
    double SecondaryScore);

public sealed record PlayerAnalysisResult(
    IReadOnlyList<PlayerAnalysisProfile> Players)
{
    public PlayerAnalysisProfile? Find(int playerId)
        => Players.FirstOrDefault(x => x.PlayerId == playerId);
}

public sealed record FormationCandidate(
    string Formation,
    IReadOnlyList<string> SlotCodes,
    double StructuralScore = 0);

public sealed record FormationCandidateSet(
    IReadOnlyList<FormationCandidate> Candidates);

public sealed record PositionAssignmentCandidate(
    string Formation,
    Lineup Lineup,
    double SuitabilityScore);

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
    double LossProbability);

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
        PositionAssignmentCandidate positionCandidate);
}

public interface IRegionalRatingCalculator
{
    RegionalRatingSnapshot Calculate(
        Lineup lineup,
        IReadOnlyList<Player> players,
        RatingContext context);
}

public interface IMatchupEvaluationEngine
{
    MatchupEvaluation Evaluate(
        RegionalRatingSnapshot own,
        RegionalRatingSnapshot opponent);
}

public interface ITacticalScoreEngine
{
    double Score(
        RegionalRatingSnapshot own,
        RegionalRatingSnapshot opponent,
        MatchupEvaluation matchup,
        MatchQuestionnaire questionnaire);
}

public interface IFinalMatchPlanEngine
{
    FinalMatchPlan Build(IReadOnlyList<TacticalCandidate> candidates);
}

public interface IMatchPredictionEngine
{
    MatchPrediction Predict(
        FinalMatchPlan plan,
        RegionalRatingSnapshot opponent,
        RatingContext context,
        MatchQuestionnaire questionnaire);
}
