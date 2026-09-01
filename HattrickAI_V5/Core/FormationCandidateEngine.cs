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
        ArgumentNullException.ThrowIfNull(players);

        var available = LegalCandidates
            .Where(candidate => HasFeasibleAssignment(players, candidate.SlotCodes))
            .Select(candidate => candidate with
            {
                StructuralScore = StructuralFeasibilityScore(players, candidate.SlotCodes)
            })
            .OrderByDescending(x => x.StructuralScore)
            .ToList();

        return new FormationCandidateSet(available);
    }

    private static bool HasFeasibleAssignment(PlayerAnalysisResult players, IReadOnlyList<string> slots)
        => TryAssign(slots, players.Players, 0, new HashSet<int>());

    private static bool TryAssign(
        IReadOnlyList<string> slots,
        IReadOnlyList<PlayerAnalysisProfile> profiles,
        int index,
        HashSet<int> used)
    {
        if (index == slots.Count) return true;

        var code = slots[index];
        foreach (var profile in profiles
            .Where(p => !used.Contains(p.PlayerId))
            .OrderByDescending(p => Score(p, code)))
        {
            if (Score(profile, code) <= 0) continue;
            used.Add(profile.PlayerId);
            if (TryAssign(slots, profiles, index + 1, used)) return true;
            used.Remove(profile.PlayerId);
        }

        return false;
    }

    private static double StructuralFeasibilityScore(
        PlayerAnalysisResult players,
        IReadOnlyList<string> slots)
    {
        // Average of the best available distinct-player scores for a slot.
        // This is only a structural quality signal, not a tactical score.
        var used = new HashSet<int>();
        var total = 0d;
        foreach (var code in slots)
        {
            var best = players.Players
                .Where(p => !used.Contains(p.PlayerId))
                .Select(p => new { Profile = p, Score = Score(p, code) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();
            if (best is null) return 0;
            used.Add(best.Profile.PlayerId);
            total += best.Score;
        }
        return total / slots.Count;
    }

    private static double Score(PlayerAnalysisProfile profile, string code)
        => profile.Positions.FirstOrDefault(x => x.PositionCode == code)?.Score ?? 0;
}
