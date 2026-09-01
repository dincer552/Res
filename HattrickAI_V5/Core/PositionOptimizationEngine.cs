using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 5 - Pozisyon Optimizasyon Motoru.
/// M4'ün verdiği yasal dizilişte, M3 oyuncu-pozisyon profillerinden en iyi
/// oyuncu-slot eşleşmelerini üretir. Rakip, taktik ve bireysel davranış seçimi
/// M5'in işi değildir; bunlar sonraki motorlarda değerlendirilir.
/// </summary>
public sealed class PositionOptimizationEngine : IPositionOptimizationEngine
{
    private const int DefaultMaxCandidates = 100;
    private const int BeamWidth = 2500;

    // M3 skoru ana sinyal olmaya devam eder. Natural-role yalnızca birbirine
    // yakın alternatifleri sıralamak için sınırlı bir etki yapar.
    private const double NaturalRoleTieThreshold = 0.75;
    private const double PrimaryRoleBonus = 0.05;
    private const double SecondaryRoleBonus = 0.02;
    private const double RoleTieEpsilon = 0.05;

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
        ValidateFormation(formation);

        var eligibleProfiles = players.Players
            .Where(p => p.IsEligible && p.PlayerId > 0)
            .GroupBy(p => p.PlayerId)
            .Select(g => g.First())
            .ToList();

        if (eligibleProfiles.Count < 11) return [];

        var byId = eligibleProfiles.ToDictionary(x => x.PlayerId);
        var results = new List<PositionAssignmentCandidate>(Math.Min(maxCandidates, BeamWidth));
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Birinci aday her zaman tüm uygun oyuncu havuzunda exact Hungarian
        // çözümüyle bulunur. Böylece candidate-pruning birinci kararı etkileyemez.
        var exact = BuildExactBestCandidate(formation, eligibleProfiles, byId);
        AddUnique(exact, results, seen);

        if (maxCandidates == 1)
            return results;

        // Önceki sürümde slot başına ilk 12 oyuncu kesiliyordu. Bu, düşük ranked
        // görünen ama başka bir slotu serbest bırakarak toplamda daha iyi olabilecek
        // oyuncuları alternatif adaylardan çıkarabiliyordu. Artık tüm eligible havuz
        // kullanılıyor; beam search yalnızca alternatif üretim maliyetini sınırlıyor.
        var slots = formation.SlotCodes
            .Select((code, index) => new SlotDefinition(index, code))
            .OrderBy(x => FeasibilityCount(eligibleProfiles, x.Code))
            .ThenByDescending(x => eligibleProfiles.Max(p => Score(p, x.Code)))
            .ToList();

        var beam = new List<PartialAssignment>
        {
            new([], new HashSet<int>(), 0d)
        };

        foreach (var slot in slots)
        {
            var next = new List<PartialAssignment>();
            foreach (var state in beam)
            {
                foreach (var profile in eligibleProfiles)
                {
                    if (state.UsedPlayerIds.Contains(profile.PlayerId)) continue;

                    var raw = Score(profile, slot.Code);
                    if (raw <= 0) continue;

                    var adjusted = AdjustedScore(profile, slot.Code);
                    var assigned = new List<AssignedSlot>(state.Assigned)
                    {
                        new(profile.PlayerId, slot.Code, raw, adjusted)
                    };
                    var used = new HashSet<int>(state.UsedPlayerIds) { profile.PlayerId };
                    next.Add(new PartialAssignment(assigned, used, state.Score + adjusted));
                }
            }

            beam = next
                .OrderByDescending(x => x.Score)
                .ThenBy(x => AssignmentKey(x.Assigned), StringComparer.Ordinal)
                .Take(BeamWidth)
                .ToList();

            if (beam.Count == 0) return results;
        }

        foreach (var state in beam
                     .OrderByDescending(x => x.Score)
                     .ThenBy(x => AssignmentKey(x.Assigned), StringComparer.Ordinal)
                     .Take(Math.Max(maxCandidates * 4, 200)))
        {
            var candidate = BuildCandidate(formation.Formation, state.Assigned, state.Score, byId);
            AddUnique(candidate, results, seen);
        }

        return results
            .OrderByDescending(x => x.SuitabilityScore)
            .ThenBy(x => AssignmentKey(x), StringComparer.Ordinal)
            .Take(maxCandidates)
            .ToList();
    }

    public IReadOnlyList<PositionAssignmentCandidate> GenerateCandidates(
        MatchDataContext context,
        PlayerAnalysisResult players,
        FormationCandidateSet formations,
        int maxCandidatesPerFormation = DefaultMaxCandidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(formations);
        if (maxCandidatesPerFormation < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCandidatesPerFormation));

        return formations.Candidates
            .SelectMany(formation => GenerateCandidates(
                context,
                players,
                formation,
                maxCandidatesPerFormation))
            .OrderByDescending(x => x.SuitabilityScore)
            .ThenBy(x => x.Formation, StringComparer.Ordinal)
            .ThenBy(x => AssignmentKey(x), StringComparer.Ordinal)
            .ToList();
    }

    private static PositionAssignmentCandidate BuildExactBestCandidate(
        FormationCandidate formation,
        IReadOnlyList<PlayerAnalysisProfile> profiles,
        IReadOnlyDictionary<int, PlayerAnalysisProfile> byId)
    {
        var rows = formation.SlotCodes.Count;
        var cols = profiles.Count;
        var cost = new double[rows, cols];

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var value = AdjustedScore(profiles[col], formation.SlotCodes[row]);
                cost[row, col] = value > 0 ? -value : 1e9;
            }
        }

        var assignment = RectangularHungarian(cost);
        var assigned = new List<AssignedSlot>(rows);
        var used = new HashSet<int>();
        var total = 0d;

        for (var row = 0; row < rows; row++)
        {
            var playerIndex = assignment[row];
            if (playerIndex < 0 || playerIndex >= profiles.Count)
                throw new InvalidOperationException(
                    $"Motor 5 '{formation.Formation}' için geçerli bir Hungarian ataması üretemedi.");

            var profile = profiles[playerIndex];
            var rawScore = Score(profile, formation.SlotCodes[row]);
            var adjustedScore = AdjustedScore(profile, formation.SlotCodes[row]);
            if (rawScore <= 0 || !used.Add(profile.PlayerId))
                throw new InvalidOperationException(
                    $"Motor 5 '{formation.Formation}' geçersiz oyuncu-slot ataması üretti.");

            assigned.Add(new AssignedSlot(profile.PlayerId, formation.SlotCodes[row], rawScore, adjustedScore));
            total += adjustedScore;
        }

        return BuildCandidate(formation.Formation, assigned, total, byId);
    }

    private static PositionAssignmentCandidate BuildCandidate(
        string formation,
        IReadOnlyList<AssignedSlot> assigned,
        double score,
        IReadOnlyDictionary<int, PlayerAnalysisProfile> profiles)
    {
        var slots = assigned
            .Select(x => ToSlot(profiles[x.PlayerId], x.Code, x.RawScore))
            .OrderBy(x => SlotOrder(x.Code))
            .ToList();

        return new PositionAssignmentCandidate(
            formation,
            new Lineup("Aday XI", formation, slots),
            score,
            assigned
                .OrderBy(x => SlotOrder(x.Code))
                .ToDictionary(x => x.PlayerId, x => x.Code));
    }

    private static void AddUnique(
        PositionAssignmentCandidate candidate,
        List<PositionAssignmentCandidate> results,
        HashSet<string> seen)
    {
        if (seen.Add(AssignmentKey(candidate)))
            results.Add(candidate);
    }

    private static int FeasibilityCount(IReadOnlyList<PlayerAnalysisProfile> profiles, string code)
        => profiles.Count(p => Score(p, code) > 0);

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

    private static double Score(PlayerAnalysisProfile profile, string code)
        => profile.Positions.FirstOrDefault(x => x.PositionCode == code)?.Score ?? 0;

    private static double AdjustedScore(PlayerAnalysisProfile profile, string code)
    {
        var raw = Score(profile, code);
        if (raw <= 0) return 0;

        var best = profile.Positions
            .Where(x => x.Score > 0)
            .Select(x => x.Score)
            .DefaultIfEmpty(0)
            .Max();

        if (best <= 0 || best - raw > NaturalRoleTieThreshold)
            return raw;

        // Eğer M3 iki veya daha fazla pozisyonu gerçekten aynı seviyede üretmişse,
        // primary/secondary etiketi sol/merkez/sağ arasında yapay fark üretmesin.
        var tiedBestCount = profile.Positions.Count(x =>
            x.Score > 0 && Math.Abs(x.Score - best) <= RoleTieEpsilon);
        if (tiedBestCount > 1)
            return raw;

        if (string.Equals(profile.PrimaryPosition, code, StringComparison.Ordinal))
            return raw + PrimaryRoleBonus;

        if (string.Equals(profile.SecondaryPosition, code, StringComparison.Ordinal))
            return raw + SecondaryRoleBonus;

        return raw;
    }

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

    private static string AssignmentKey(IReadOnlyList<AssignedSlot> assigned)
        => string.Join(
            ";",
            assigned
                .OrderBy(x => SlotOrder(x.Code))
                .Select(x => $"{x.Code}:{x.PlayerId}"));

    private static int[] RectangularHungarian(double[,] cost)
    {
        var rows = cost.GetLength(0);
        var cols = cost.GetLength(1);
        if (rows > cols) throw new ArgumentException("Satır sayısı sütun sayısından büyük olamaz.");

        var u = new double[rows + 1];
        var v = new double[cols + 1];
        var p = new int[cols + 1];
        var way = new int[cols + 1];

        for (var i = 1; i <= rows; i++)
        {
            p[0] = i;
            var j0 = 0;
            var minv = Enumerable.Repeat(double.PositiveInfinity, cols + 1).ToArray();
            var used = new bool[cols + 1];

            do
            {
                used[j0] = true;
                var i0 = p[j0];
                var delta = double.PositiveInfinity;
                var j1 = 0;

                for (var j = 1; j <= cols; j++)
                {
                    if (used[j]) continue;
                    var cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                if (double.IsPositiveInfinity(delta))
                    throw new InvalidOperationException("Motor 5 Hungarian optimizasyonunda geçerli yol bulunamadı.");

                for (var j = 0; j <= cols; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (p[j0] != 0);

            do
            {
                var j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        var assignment = Enumerable.Repeat(-1, rows).ToArray();
        for (var j = 1; j <= cols; j++)
        {
            if (p[j] > 0)
                assignment[p[j] - 1] = j - 1;
        }

        return assignment;
    }

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

    private sealed record SlotDefinition(int Index, string Code);
    private sealed record PartialAssignment(
        IReadOnlyList<AssignedSlot> Assigned,
        IReadOnlySet<int> UsedPlayerIds,
        double Score);
    private sealed record AssignedSlot(int PlayerId, string Code, double RawScore, double AdjustedScore);
}