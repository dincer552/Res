namespace HattrickAI.V5.Core;

/// <summary>
/// M6: generates individual-behaviour candidates for a fixed XI.
/// M6 does not select a winner and does not inspect the opponent.
/// Rating, matchup and tactical value are evaluated by later motors.
/// </summary>
public sealed class BehaviourCandidateEngine
{
    private readonly BehaviourEngine _behaviourEngine;

    public BehaviourCandidateEngine()
        : this(new BehaviourEngine())
    {
    }

    public BehaviourCandidateEngine(BehaviourEngine behaviourEngine)
    {
        _behaviourEngine = behaviourEngine ?? throw new ArgumentNullException(nameof(behaviourEngine));
    }

    /// <summary>
    /// Returns the legal order options for every field slot in the XI.
    /// The normal order is always retained as the baseline.
    /// </summary>
    public BehaviourCandidateSet Build(Lineup lineup, IReadOnlyList<Player> players)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);

        var eligibleIds = players
            .Where(p => p.Id > 0 && p.InjuryLevel != 999)
            .Select(p => p.Id)
            .ToHashSet();

        var options = new List<BehaviourSlotOptions>();

        foreach (var slot in lineup.Slots.Where(s => s.PlayerId > 0))
        {
            // M6 never creates a candidate for an unavailable player.
            if (!eligibleIds.Contains(slot.PlayerId))
                continue;

            var playerName = slot.PlayerName
                ?? players.FirstOrDefault(p => p.Id == slot.PlayerId)?.Name
                ?? $"Player {slot.PlayerId}";

            var orders = _behaviourEngine
                .GetAllowedOrders(slot.Code)
                .Distinct()
                .ToArray();

            if (!orders.Contains(PlayerOrder.Normal))
                orders = [PlayerOrder.Normal, .. orders];

            options.Add(new BehaviourSlotOptions(
                slot.Code,
                slot.PlayerId,
                playerName,
                orders));
        }

        return new BehaviourCandidateSet(
            lineup.Formation,
            options,
            CalculateCombinationCount(options));
    }

    /// <summary>
    /// Produces complete behaviour sets only when the Cartesian product is
    /// reasonably bounded. This method is intentionally not a scorer.
    /// For larger spaces, callers should keep the per-slot candidate matrix
    /// and let M7/M10 perform evidence-based evaluation and pruning.
    /// </summary>
    public IReadOnlyList<BehaviourSetCandidate> EnumerateCompleteSets(
        BehaviourCandidateSet candidateSet,
        int maxSets = 100_000)
    {
        ArgumentNullException.ThrowIfNull(candidateSet);
        if (maxSets < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSets));

        if (candidateSet.CombinationCount > maxSets)
            return Array.Empty<BehaviourSetCandidate>();

        var results = new List<BehaviourSetCandidate>((int)candidateSet.CombinationCount);
        var buffer = new List<BehaviourAssignment>(candidateSet.Slots.Count);

        void Walk(int index)
        {
            if (index == candidateSet.Slots.Count)
            {
                results.Add(new BehaviourSetCandidate(
                    candidateSet.Formation,
                    buffer.ToArray()));
                return;
            }

            var slot = candidateSet.Slots[index];
            foreach (var order in slot.AllowedOrders)
            {
                buffer.Add(new BehaviourAssignment(
                    slot.PlayerId,
                    slot.PlayerName,
                    slot.PositionCode,
                    order));
                Walk(index + 1);
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        Walk(0);
        return results;
    }

    private static long CalculateCombinationCount(IReadOnlyList<BehaviourSlotOptions> slots)
    {
        long count = 1;
        foreach (var slot in slots)
        {
            if (slot.AllowedOrders.Count == 0)
                return 0;

            if (count > long.MaxValue / slot.AllowedOrders.Count)
                return long.MaxValue;

            count *= slot.AllowedOrders.Count;
        }

        return count;
    }
}

public sealed record BehaviourCandidateSet(
    string Formation,
    IReadOnlyList<BehaviourSlotOptions> Slots,
    long CombinationCount)
{
    public bool IsExhaustivelyEnumerable(int maxSets = 100_000)
        => CombinationCount <= maxSets;
}

public sealed record BehaviourSlotOptions(
    string PositionCode,
    int PlayerId,
    string PlayerName,
    IReadOnlyList<PlayerOrder> AllowedOrders);

public sealed record BehaviourAssignment(
    int PlayerId,
    string PlayerName,
    string PositionCode,
    PlayerOrder Order);

public sealed record BehaviourSetCandidate(
    string Formation,
    IReadOnlyList<BehaviourAssignment> Assignments);
