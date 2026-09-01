namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 5 - Pozisyon Optimizasyon Motoru.
/// Motor 4'ten gelen herhangi bir yasal dizilişi, Motor 3'ün oyuncu-pozisyon
/// skorlarıyla doldurur. Her oyuncu aday XI içinde en fazla bir kez kullanılır.
/// Rakip/taktik değerlendirme ve bireysel emir seçimi bu motorun sorumluluğu değildir.
/// </summary>
public sealed class PositionOptimizationEngine : IPositionOptimizationEngine
{
    private const int DefaultMaxCandidates = 100;
    private const int PlayerPoolPerSlot = 12;

    public IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidate formation)
        => GenerateCandidates(context, players, formation, DefaultMaxCandidates);

    /// <summary>
    /// Motor 4'ün ürettiği tek bir FormationCandidate için en iyi oyuncu-slot
    /// kombinasyonlarını üretir.
    /// </summary>
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
        ValidateFormation(formation);

        // Motor 3'ün eligibility kararına tekrar güvenlik filtresi koyuyoruz.
        // Böylece eski/harici profile verisi sakat oyuncuyu M5'e sızdıramaz.
        var eligibleProfiles = players.Players
            .Where(p => p.IsEligible && p.PlayerId > 0)
            .GroupBy(p => p.PlayerId)
            .Select(g => g.First())
            .ToList();

        if (eligibleProfiles.Count < 11) return [];

        var byId = eligibleProfiles.ToDictionary(x => x.PlayerId);
        var candidatePools = formation.SlotCodes
            .Select(code => new SlotPool(
                code,
                eligibleProfiles
                    .Select(p => new PlayerSlotScore(p.PlayerId, Score(p, code)))
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.PlayerId)
                    .Take(PlayerPoolPerSlot)
                    .ToList()))
            .ToList();

        // Tek bir slot bile doldurulamıyorsa diziliş Motor 5'ten geçmez.
        if (candidatePools.Any(x => x.Players.Count == 0)) return [];

        // Önce seçenek sayısı en az olan slotları doldurmak dallanmayı azaltır.
        var ordered = candidatePools
            .OrderBy(x => x.Players.Count)
            .ThenByDescending(x => x.Players.Max(y => y.Score))
            .ToList();

        var results = new List<PositionAssignmentCandidate>();
        Search(
            ordered,
            0,
            new HashSet<int>(),
            new List<AssignedSlot>(),
            0d,
            byId,
            results,
            resultLimit: Math.Max(maxCandidates * 8, 100),
            formation.Formation);

        return results
            .OrderByDescending(x => x.SuitabilityScore)
            .ThenBy(x => AssignmentKey(x))
            .Take(maxCandidates)
            .ToList();
    }

    /// <summary>
    /// Motor 4'ün tüm aday diziliş setini doğrudan Motor 5'e bağlayan kolaylık API'si.
    /// Sonuçta her diziliş için bağımsız adaylar döner; Motor 5 diziliş seçmez.
    /// </summary>
    public IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidateSet formations,
        int maxCandidatesPerFormation = DefaultMaxCandidates)
    {
        ArgumentNullException.ThrowIfNull(formations);
        if (maxCandidatesPerFormation < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCandidatesPerFormation));

        return formations.Candidates
            .SelectMany(formation => GenerateCandidates(context, players, formation, maxCandidatesPerFormation))
            .OrderByDescending(x => x.SuitabilityScore)
            .ThenBy(x => x.Formation, StringComparer.Ordinal)
            .Take(maxCandidatesPerFormation * Math.Max(1, formations.Candidates.Count))
            .ToList();
    }

    private static void ValidateFormation(FormationCandidate formation)
    {
        if (string.IsNullOrWhiteSpace(formation.Formation))
            throw new ArgumentException("Diziliş adı boş olamaz.", nameof(formation));

        if (formation.SlotCodes.Count != 11)
            throw new ArgumentException(
                $"Motor 5 için '{formation.Formation}' dizilişi tam 11 slot içermelidir.",
                nameof(formation));

        if (formation.SlotCodes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                $"'{formation.Formation}' dizilişinde boş slot kodu bulunamaz.",
                nameof(formation));

        if (formation.SlotCodes.Distinct(StringComparer.Ordinal).Count() != formation.SlotCodes.Count)
            throw new ArgumentException(
                $"'{formation.Formation}' dizilişinde tekrar eden slot kodu bulunamaz.",
                nameof(formation));
    }

    private static void Search(
        IReadOnlyList<SlotPool> pools,
        int index,
        HashSet<int> used,
        List<AssignedSlot> assigned,
        double score,
        IReadOnlyDictionary<int, PlayerAnalysisProfile> profiles,
        List<PositionAssignmentCandidate> results,
        int resultLimit,
        string formation)
    {
        if (results.Count >= resultLimit) return;

        if (index == pools.Count)
        {
            var slots = assigned
                .Select(x => ToSlot(profiles[x.PlayerId], x.Code, x.Score))
                .OrderBy(x => SlotOrder(x.Code))
                .ToList();

            results.Add(new PositionAssignmentCandidate(
                formation,
                new Lineup("Aday XI", formation, slots),
                score,
                assigned
                    .OrderBy(x => SlotOrder(x.Code))
                    .ToDictionary(x => x.PlayerId, x => x.Code)));
            return;
        }

        var pool = pools[index];
        foreach (var option in pool.Players)
        {
            if (used.Contains(option.PlayerId)) continue;

            used.Add(option.PlayerId);
            assigned.Add(new AssignedSlot(option.PlayerId, pool.Code, option.Score));
            Search(
                pools,
                index + 1,
                used,
                assigned,
                score + option.Score,
                profiles,
                results,
                resultLimit,
                formation);
            assigned.RemoveAt(assigned.Count - 1);
            used.Remove(option.PlayerId);

            if (results.Count >= resultLimit) return;
        }
    }

    private static double Score(PlayerAnalysisProfile profile, string code)
        => profile.Positions.FirstOrDefault(x => x.PositionCode == code)?.Score ?? 0;

    private static Slot ToSlot(PlayerAnalysisProfile profile, string code, double score)
    {
        var (x, y, label, description) = SlotPresentation(code);
        return new Slot(
            code,
            label,
            description,
            profile.PlayerName,
            profile.PlayerId,
            score,
            x,
            y,
            PlayerOrder.Normal);
    }

    private static string AssignmentKey(PositionAssignmentCandidate candidate)
        => string.Join(
            ";",
            candidate.Lineup.Slots
                .OrderBy(x => SlotOrder(x.Code))
                .Select(x => $"{x.Code}:{x.PlayerId}"));

    private static int SlotOrder(string code) => code switch
    {
        "GK" => 0,
        "DEF-L" => 10, "DEF-CL" => 11, "DEF-C" => 12, "DEF-CR" => 13, "DEF-R" => 14,
        "W-L" => 20, "IM-L" => 21, "IM-C" => 22, "IM-R" => 23, "W-R" => 24,
        "FW-L" => 30, "FW-C" => 31, "FW-R" => 32,
        _ => 99
    };

    private static (double X, double Y, string Label, string Description) SlotPresentation(string code) => code switch
    {
        "GK" => (50, 10, "GK", "Kaleci"),
        "DEF-L" => (12, 34, "DEF-L", "Sol bek"),
        "DEF-CL" => (30, 34, "DEF-CL", "Sol stoper"),
        "DEF-C" => (50, 34, "DEF-C", "Merkez stoper"),
        "DEF-CR" => (70, 34, "DEF-CR", "Sağ stoper"),
        "DEF-R" => (88, 34, "DEF-R", "Sağ bek"),
        "W-L" => (12, 50, "W-L", "Sol kanat"),
        "IM-L" => (34, 50, "IM-L", "Sol iç"),
        "IM-C" => (50, 50, "IM-C", "Merkez"),
        "IM-R" => (66, 50, "IM-R", "Sağ iç"),
        "W-R" => (88, 50, "W-R", "Sağ kanat"),
        "FW-L" => (38, 72, "FW-L", "Sol forvet"),
        "FW-C" => (50, 72, "FW-C", "Merkez forvet"),
        "FW-R" => (62, 72, "FW-R", "Sağ forvet"),
        _ => (50, 50, code, code)
    };

    private sealed record SlotPool(string Code, IReadOnlyList<PlayerSlotScore> Players);
    private sealed record PlayerSlotScore(int PlayerId, double Score);
    private sealed record AssignedSlot(int PlayerId, string Code, double Score);
}
