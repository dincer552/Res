namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 5 - Pozisyon Optimizasyon Motoru.
/// Motor 4'ten gelen bir dizilişi, Motor 3'ün oyuncu-pozisyon skorlarıyla
/// doldurur. Her oyuncu en fazla bir slotta kullanılabilir.
/// Rakip taktik skoru ve bireysel emir seçimi bu motorun sorumluluğu değildir.
/// </summary>
public sealed class PositionOptimizationEngine : IPositionOptimizationEngine
{
    private const int DefaultMaxCandidates = 100;

    public IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidate formation)
        => GenerateCandidates(context, players, formation, DefaultMaxCandidates);

    public IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidate formation,
        int maxCandidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(formation);
        if (maxCandidates < 1) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        if (formation.SlotCodes.Count != 11) return [];

        var byId = players.Players.ToDictionary(x => x.PlayerId);
        var candidatePools = formation.SlotCodes
            .Select(code => new SlotPool(code,
                players.Players
                    .Select(p => new PlayerSlotScore(p.PlayerId, Score(p, code)))
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(Math.Max(8, maxCandidates / 4))
                    .ToList()))
            .ToList();

        if (candidatePools.Any(x => x.Players.Count == 0)) return [];

        // En az seçenekli slotları önce doldurmak dallanmayı azaltır.
        var ordered = candidatePools
            .OrderBy(x => x.Players.Count)
            .ThenBy(x => x.Players.Max(y => y.Score))
            .ToList();

        var results = new List<PositionAssignmentCandidate>();
        Search(ordered, 0, new HashSet<int>(), new List<AssignedSlot>(), 0d, byId, results, maxCandidates * 4);

        return results
            .OrderByDescending(x => x.SuitabilityScore)
            .Take(maxCandidates)
            .ToList();
    }

    private static void Search(
        IReadOnlyList<SlotPool> pools,
        int index,
        HashSet<int> used,
        List<AssignedSlot> assigned,
        double score,
        IReadOnlyDictionary<int, PlayerAnalysisProfile> profiles,
        List<PositionAssignmentCandidate> results,
        int resultLimit)
    {
        if (results.Count >= resultLimit) return;

        if (index == pools.Count)
        {
            var slots = assigned
                .Select(x => ToSlot(profiles[x.PlayerId], x.Code, x.Score))
                .OrderBy(x => SlotOrder(x.Code))
                .ToList();

            results.Add(new PositionAssignmentCandidate(
                pools.Count == 0 ? string.Empty : "",
                new Lineup("Aday XI", string.Empty, slots),
                score));
            return;
        }

        var pool = pools[index];
        foreach (var option in pool.Players)
        {
            if (used.Contains(option.PlayerId)) continue;

            used.Add(option.PlayerId);
            assigned.Add(new AssignedSlot(option.PlayerId, pool.Code, option.Score));
            Search(pools, index + 1, used, assigned, score + option.Score, profiles, results, resultLimit);
            assigned.RemoveAt(assigned.Count - 1);
            used.Remove(option.PlayerId);

            if (results.Count >= resultLimit) return;
        }
    }

    private static double Score(PlayerAnalysisProfile profile, string code)
        => profile.Positions.FirstOrDefault(x => x.PositionCode == code)?.Score ?? 0;

    private static Slot ToSlot(PlayerAnalysisProfile profile, string code, double score)
    {
        var (x, y) = Coordinates(code);
        return new Slot(code, code, code, profile.PlayerName, profile.PlayerId, score, x, y, PlayerOrder.Normal);
    }

    private static int SlotOrder(string code) => code switch
    {
        "GK" => 0,
        "DEF-L" => 10,
        "DEF-CL" => 11,
        "DEF-C" => 12,
        "DEF-CR" => 13,
        "DEF-R" => 14,
        "W-L" => 20,
        "IM-L" => 21,
        "IM-C" => 22,
        "IM-R" => 23,
        "W-R" => 24,
        "FW-L" => 30,
        "FW-C" => 31,
        "FW-R" => 32,
        _ => 99
    };

    private static (double X, double Y) Coordinates(string code) => code switch
    {
        "GK" => (50, 10),
        "DEF-L" => (12, 34), "DEF-CL" => (30, 34), "DEF-C" => (50, 34),
        "DEF-CR" => (70, 34), "DEF-R" => (88, 34),
        "W-L" => (12, 50), "IM-L" => (34, 50), "IM-C" => (50, 50),
        "IM-R" => (66, 50), "W-R" => (88, 50),
        "FW-L" => (38, 72), "FW-C" => (50, 72), "FW-R" => (62, 72),
        _ => (50, 50)
    };

    private sealed record SlotPool(string Code, IReadOnlyList<PlayerSlotScore> Players);
    private sealed record PlayerSlotScore(int PlayerId, double Score);
    private sealed record AssignedSlot(int PlayerId, string Code, double Score);
}
