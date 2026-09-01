namespace HattrickAI.V5.Core;

/// <summary>
/// Compatibility facade for M6.
/// The canonical M6 implementation is BehaviourCandidateEngine.
/// This class intentionally does not inspect the opponent or select a winner.
/// Selection belongs to M7/M8/M9/M10 after rating and matchup evaluation.
/// </summary>
[Obsolete("Use BehaviourCandidateEngine for M6 candidate generation. Final behaviour selection belongs to M10.")]
public sealed class BehaviourOptimizer
{
    private readonly BehaviourCandidateEngine _candidateEngine;

    public BehaviourOptimizer()
        : this(new BehaviourCandidateEngine())
    {
    }

    public BehaviourOptimizer(BehaviourCandidateEngine candidateEngine)
    {
        _candidateEngine = candidateEngine ?? throw new ArgumentNullException(nameof(candidateEngine));
    }

    public BehaviourCandidateSet GenerateCandidates(Lineup lineup, IReadOnlyList<Player> players)
        => _candidateEngine.Build(lineup, players);

    /// <summary>
    /// Backward-compatible entry point. It no longer performs opponent-aware
    /// scoring. It returns the candidate matrix and uses Normal only as a
    /// compatibility baseline; callers must not treat this as the final decision.
    /// </summary>
    public BehaviourDecision Choose(Player player, Slot slot, Lineup lineup, IReadOnlyList<Player> players,
        RegionalRatingSnapshot opponentRating, RatingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);

        var allowed = new BehaviourEngine().GetAllowedOrders(slot.Code);
        var normal = allowed.Contains(PlayerOrder.Normal) ? PlayerOrder.Normal : allowed[0];

        return new BehaviourDecision(
            player.Id,
            player.Name,
            slot.Code,
            normal,
            0,
            "Compatibility baseline only. M6 does not select a final behaviour; M7-M10 must evaluate candidates.");
    }
}

public sealed record BehaviourDecision(
    int PlayerId,
    string PlayerName,
    string PositionCode,
    PlayerOrder Order,
    double Score,
    string Reason);
