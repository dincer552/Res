namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 4 - Aday Diziliş Motoru.
/// Sadece yasal ve doldurulabilir diziliş adaylarını üretir. Oyuncu/slot
/// eşleştirmesi Motor 5'e, rakibe karşı skor Motor 8-9'a bırakılır.
/// </summary>
public sealed class FormationCandidateEngine : IFormationCandidateEngine
{
    private static readonly IReadOnlyList<FormationCandidate> LegalCandidates =
    [
        new("3-5-2", ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-R"]),
        new("3-4-3", ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"]),
        new("4-4-2", ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-R"]),
        new("4-5-1", ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-C"]),
        new("2-5-3", ["GK", "DEF-CL", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"]),
        new("5-3-2", ["GK", "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R", "IM-L", "IM-C", "IM-R", "FW-L", "FW-R"])
    ];

    public FormationCandidateSet Generate(MatchDataContext context, PlayerAnalysisResult players)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Generate(players);
    }

    // M4 is structural only. Its feasibility and structural score use the same
    // eligible player universe that M5 is allowed to consume.
    public FormationCandidateSet Generate(PlayerAnalysisResult players)
    {
        ArgumentNullException.ThrowIfNull(players);

        var eligiblePlayers = players.Players
            .Where(p => p.IsEligible && p.PlayerId > 0)
            .GroupBy(p => p.PlayerId)
            .Select(g => g.First())
            .ToList();

        var available = LegalCandidates
            .Where(candidate => HasFeasibleAssignment(eligiblePlayers, candidate.SlotCodes))
            .Select(candidate => candidate with
            {
                StructuralScore = StructuralFeasibilityScore(eligiblePlayers, candidate.SlotCodes)
            })
            .Where(candidate => double.IsFinite(candidate.StructuralScore) && candidate.StructuralScore > 0)
            .OrderByDescending(x => x.StructuralScore)
            .ThenBy(x => x.Formation, StringComparer.Ordinal)
            .ToList();

        return new FormationCandidateSet(available);
    }

    private static bool HasFeasibleAssignment(
        IReadOnlyList<PlayerAnalysisProfile> players,
        IReadOnlyList<string> slots)
        => TryAssign(slots, players, 0, new HashSet<int>());

    private static bool TryAssign(
        IReadOnlyList<string> slots,
        IReadOnlyList<PlayerAnalysisProfile> profiles,
        int index,
        HashSet<int> used)
    {
        if (index == slots.Count) return true;

        var remaining = slots.Skip(index).ToList();
        var next = remaining
            .Select(code => new
            {
                Code = code,
                Count = profiles.Count(p => !used.Contains(p.PlayerId) && Score(p, code) > 0)
            })
            .OrderBy(x => x.Count)
            .ThenBy(x => PositionOrder(x.Code))
            .First();

        if (next.Count == 0) return false;

        foreach (var profile in profiles
            .Where(p => !used.Contains(p.PlayerId) && Score(p, next.Code) > 0)
            .OrderByDescending(p => Score(p, next.Code))
            .ThenBy(p => p.PlayerId))
        {
            used.Add(profile.PlayerId);
            var reordered = remaining;
            reordered.Remove(next.Code);
            if (TryAssign(reordered, profiles, 0, used)) return true;
            used.Remove(profile.PlayerId);
        }

        return false;
    }

    private static double StructuralFeasibilityScore(
        IReadOnlyList<PlayerAnalysisProfile> players,
        IReadOnlyList<string> slots)
    {
        var remaining = slots.ToList();
        var used = new HashSet<int>();
        var total = 0d;

        while (remaining.Count > 0)
        {
            var next = remaining
                .Select(code => new
                {
                    Code = code,
                    Count = players.Count(p => !used.Contains(p.PlayerId) && Score(p, code) > 0)
                })
                .OrderBy(x => x.Count)
                .ThenBy(x => PositionOrder(x.Code))
                .First();

            var best = players
                .Where(p => !used.Contains(p.PlayerId))
                .Select(p => new { Profile = p, Score = Score(p, next.Code) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Profile.PlayerId)
                .FirstOrDefault();

            if (best is null) return 0;
            used.Add(best.Profile.PlayerId);
            total += best.Score;
            remaining.Remove(next.Code);
        }

        return total / slots.Count;
    }

    private static double Score(PlayerAnalysisProfile profile, string code)
        => profile.Positions.FirstOrDefault(x => x.PositionCode == code)?.Score ?? 0;

    private static int PositionOrder(string code) => code switch
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
}
