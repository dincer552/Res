using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Tek analiz oturumu içindeki aday değerlendirmelerini sınırlar ve deterministik
/// biçimde sıralar. Kalıcı ML modeli değildir; M6/M10/M11 arasındaki arama havuzudur.
/// </summary>
public sealed class CandidateEvaluationDatabase
{
    public const int DefaultCapacity = 100;
    public const int MaxPerFormation = 30;

    private readonly Dictionary<string, CandidateEvaluationRecord> _records = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requiredFormations;

    public CandidateEvaluationDatabase(
        string name,
        int capacity = DefaultCapacity,
        IReadOnlyCollection<string>? requiredFormations = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Database adı boş olamaz.", nameof(name));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name;
        Capacity = capacity;
        _requiredFormations = requiredFormations is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : requiredFormations
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.Ordinal);
    }

    public string Name { get; }
    public int Capacity { get; }
    public IReadOnlyCollection<string> RequiredFormations => _requiredFormations;
    public IReadOnlyList<CandidateEvaluationRecord> Records => _records.Values
        .OrderByDescending(x => x.RankingScore)
        .ThenBy(x => x.Formation, StringComparer.Ordinal)
        .ThenBy(x => x.CandidateId, StringComparer.Ordinal)
        .ToList();

    public int Count => _records.Count;

    public IReadOnlyDictionary<string, int> FormationCounts =>
        _records.Values
            .GroupBy(x => x.Formation, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    public void Add(CandidateEvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.CandidateId)) return;
        if (!double.IsFinite(record.RankingScore)) return;

        if (_records.TryGetValue(record.CandidateId, out var existing) && existing.RankingScore >= record.RankingScore)
            return;

        _records[record.CandidateId] = record;
        Trim();
    }

    public void AddRange(IEnumerable<CandidateEvaluationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records) Add(record);
    }

    public IReadOnlyList<CandidateEvaluationRecord> Top(int count) => Records.Take(Math.Max(0, count)).ToList();

    /// <summary>
    /// Önce her mevcut formasyondan en iyi adayı garanti eder, ardından kalan
    /// kapasiteyi global ranking sırasıyla doldurur. RequiredFormations verilmişse
    /// o formasyonların tamamı havuzda kalır; böylece erken global trim formasyonu
    /// arama yarışından silemez.
    /// </summary>
    public IReadOnlyList<CandidateEvaluationRecord> TopWithFormationDiversity(int count, int maxPerFormation = MaxPerFormation)
    {
        if (count < 1) return [];
        if (maxPerFormation < 1) throw new ArgumentOutOfRangeException(nameof(maxPerFormation));

        var ordered = Records;
        var selected = new List<CandidateEvaluationRecord>(Math.Min(count, Capacity));
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        var perFormation = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var formation in _requiredFormations.OrderBy(x => x, StringComparer.Ordinal))
        {
            var first = ordered.FirstOrDefault(x => x.Formation.Equals(formation, StringComparison.Ordinal));
            if (first is null || selected.Count >= count) continue;
            selected.Add(first);
            selectedIds.Add(first.CandidateId);
            perFormation[first.Formation] = 1;
        }

        foreach (var formationGroup in ordered
                     .Where(x => !_requiredFormations.Contains(x.Formation))
                     .GroupBy(x => x.Formation, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (selected.Count >= count) break;
            var first = formationGroup.First();
            if (selectedIds.Add(first.CandidateId))
            {
                selected.Add(first);
                perFormation[first.Formation] = 1;
            }
        }

        foreach (var record in ordered)
        {
            if (selected.Count >= count || selectedIds.Contains(record.CandidateId)) continue;
            perFormation.TryGetValue(record.Formation, out var formationCount);
            if (formationCount >= maxPerFormation) continue;
            selected.Add(record);
            selectedIds.Add(record.CandidateId);
            perFormation[record.Formation] = formationCount + 1;
        }

        return selected;
    }

    public void Clear() => _records.Clear();

    private void Trim()
    {
        if (_records.Count <= Capacity) return;

        // Anti-lock is enforced at insertion time, not only when a consumer
        // later asks for a diverse Top() list. Reserve the strongest record for
        // every required formation before filling the remaining capacity globally.
        var ordered = _records.Values
            .OrderByDescending(x => x.RankingScore)
            .ThenBy(x => x.Formation, StringComparer.Ordinal)
            .ThenBy(x => x.CandidateId, StringComparer.Ordinal)
            .ToList();

        var reservedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var formation in _requiredFormations)
        {
            var best = ordered.FirstOrDefault(x => x.Formation.Equals(formation, StringComparison.Ordinal));
            if (best is not null) reservedIds.Add(best.CandidateId);
        }

        // Safety: when the database is configured with more required formations
        // than its capacity, global ordering decides the remainder after the
        // deterministic reservation pass.
        var keep = ordered.Where(x => reservedIds.Contains(x.CandidateId)).ToList();
        keep.AddRange(ordered.Where(x => !reservedIds.Contains(x.CandidateId))
            .Take(Math.Max(0, Capacity - keep.Count)));

        var keepIds = keep.Select(x => x.CandidateId).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _records.Keys.Where(x => !keepIds.Contains(x)).ToList())
            _records.Remove(key);
    }
}

public sealed record CandidateEvaluationRecord(
    string CandidateId,
    string Formation,
    Lineup Lineup,
    double M5SuitabilityScore,
    double M5StructuralScore,
    double TacticalScore,
    RegionalRatingSnapshot Rating,
    AdvancedTacticalScenarioResult Advanced,
    M8ChanceResult Chance,
    MatchPrediction? Prediction,
    double RankingScore,
    string Stage);

/// <summary>
/// Bir analiz çalışmasının iki ayrı arama havuzunu birlikte taşır.
/// Database #1 ilk M6 aramasından, Database #2 ikinci M6-B aramasından oluşur.
/// </summary>
public sealed class CandidateDatabaseSet
{
    public CandidateDatabaseSet(
        int capacity = CandidateEvaluationDatabase.DefaultCapacity,
        IReadOnlyCollection<string>? requiredFormations = null)
    {
        FirstPass = new CandidateEvaluationDatabase("Candidate Database #1", capacity, requiredFormations);
        SecondPass = new CandidateEvaluationDatabase("Candidate Database #2", capacity, requiredFormations);
    }

    public CandidateEvaluationDatabase FirstPass { get; }
    public CandidateEvaluationDatabase SecondPass { get; }
}
