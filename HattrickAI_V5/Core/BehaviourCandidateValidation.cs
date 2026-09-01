namespace HattrickAI.V5.Core;

/// <summary>
/// M6 validation layer. It verifies that a behaviour candidate matrix is
/// complete, legal and deterministic without deciding which behaviour wins.
/// </summary>
public static class BehaviourCandidateValidation
{
    public static BehaviourValidationResult Validate(
        Lineup lineup,
        IReadOnlyList<Player> players,
        BehaviourCandidateSet candidateSet)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(candidateSet);

        var errors = new List<string>();
        var warnings = new List<string>();
        var eligible = players.Where(p => p.Id > 0 && p.InjuryLevel != 999)
            .Select(p => p.Id).ToHashSet();

        if (!string.Equals(lineup.Formation, candidateSet.Formation, StringComparison.Ordinal))
            errors.Add("Formation mismatch between XI and M6 candidate set.");

        var activeSlots = lineup.Slots.Where(s => s.PlayerId > 0).ToList();
        var seenSlots = new HashSet<string>(StringComparer.Ordinal);
        var seenPlayers = new HashSet<int>();

        foreach (var slot in activeSlots)
        {
            if (!seenSlots.Add(slot.Code))
                warnings.Add($"Duplicate slot code detected: {slot.Code}.");
            if (!seenPlayers.Add(slot.PlayerId))
                errors.Add($"Duplicate player in XI: {slot.PlayerId}.");
            if (!eligible.Contains(slot.PlayerId))
                errors.Add($"Ineligible or missing player exposed in XI: {slot.PlayerId}.");

            var option = candidateSet.Slots.FirstOrDefault(x => x.PositionCode == slot.Code && x.PlayerId == slot.PlayerId);
            if (option is null)
            {
                errors.Add($"Missing M6 option matrix entry: {slot.Code}/{slot.PlayerId}.");
                continue;
            }

            if (option.AllowedOrders.Count == 0)
                errors.Add($"No legal behaviour options: {slot.Code}/{slot.PlayerId}.");
            if (!option.AllowedOrders.Contains(PlayerOrder.Normal))
                errors.Add($"Normal baseline missing: {slot.Code}/{slot.PlayerId}.");
            if (option.AllowedOrders.Distinct().Count() != option.AllowedOrders.Count)
                errors.Add($"Duplicate behaviour option: {slot.Code}/{slot.PlayerId}.");
        }

        var expectedCount = CalculateCombinationCount(candidateSet.Slots);
        if (expectedCount != candidateSet.CombinationCount)
            errors.Add($"Combination count mismatch: declared={candidateSet.CombinationCount}, calculated={expectedCount}.");

        if (candidateSet.CombinationCount > 100_000)
            warnings.Add("Cartesian product is large; complete enumeration must be delegated to downstream evaluation/pruning.");

        return new BehaviourValidationResult(
            errors.Count == 0,
            candidateSet.CombinationCount,
            activeSlots.Count,
            candidateSet.Slots.Count,
            errors,
            warnings);
    }

    private static long CalculateCombinationCount(IReadOnlyList<BehaviourSlotOptions> slots)
    {
        long count = 1;
        foreach (var slot in slots)
        {
            if (slot.AllowedOrders.Count == 0) return 0;
            if (count > long.MaxValue / slot.AllowedOrders.Count) return long.MaxValue;
            count *= slot.AllowedOrders.Count;
        }
        return count;
    }
}

public sealed record BehaviourValidationResult(
    bool IsValid,
    long CombinationCount,
    int ActiveSlotCount,
    int CandidateSlotCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
