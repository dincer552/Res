namespace HattrickAI.V5.Core;

/// <summary>
/// Compatibility adapter for the existing XI optimizer.
/// Canonical player-position analysis now lives in PlayerAnalysisEngine (Motor 3).
/// </summary>
public sealed class PositionSuitabilityEngine
{
    private readonly PlayerAnalysisEngine _playerAnalysis = new();

    public double Score(Player player, string positionCode)
        => _playerAnalysis.Score(player, positionCode);

    public IReadOnlyDictionary<string, double> ScoreAll(Player player)
        => _playerAnalysis.AnalyzePlayer(player).Positions
            .ToDictionary(x => x.PositionCode, x => x.Score);

    public PlayerAnalysisProfile Analyze(Player player)
        => _playerAnalysis.AnalyzePlayer(player);

    public IReadOnlyList<PlayerAnalysisProfile> AnalyzeAll(IEnumerable<Player> players)
        => players.Select(_playerAnalysis.AnalyzePlayer).ToList();
}
