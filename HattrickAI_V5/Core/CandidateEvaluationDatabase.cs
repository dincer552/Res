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

    public CandidateEvaluationDatabase(string name, int capacity = DefaultCapacity)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Database adı boş olamaz.", nameof(name));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name;
        Capacity = capacity;
    }

    public string Name { get; }
    public int Capacity { get; }
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
    /// kapasiteyi global ranking sırasıyla doldurur. Böylece yüksek skorlu tek
    /// bir formasyon diğer legal formasyonları DB1/DB2'den tamamen silemez.
    /// </summary>
    public IReadOnlyList<CandidateEvaluationRecord> TopWithFormationDiversity(int count, int maxPerFormation = MaxPerFormation)
    {
        if (count < 1) return [];
        if (maxPerFormation < 1) throw new ArgumentOutOfRangeException(nameof(maxPerFormation));

        var ordered = Records;
        var selected = new List<CandidateEvaluationRecord>(Math.Min(count, Capacity));
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        var perFormation = new Dictionary<string, int>(StringComparer.Ordinal);

        // Anti-lock: first reserve the strongest candidate of every formation.
        foreach (var formationGroup in ordered.GroupBy(x => x.Formation, StringComparer.Ordinal)
                                               .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var first = formationGroup.First();
            if (selected.Count >= count) break;
            selected.Add(first);
            selectedIds.Add(first.CandidateId);
            perFormation[first.Formation] = 1;
        }

        // Fill remaining slots globally while preserving the per-formation cap.
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
        foreach (var key in _records.Values
                     .OrderByDescending(x => x.RankingScore)
                     .ThenBy(x => x.Formation, StringComparer.Ordinal)
                     .ThenBy(x => x.CandidateId, StringComparer.Ordinal)
                     .Skip(Capacity)
                     .Select(x => x.CandidateId)
                     .ToList())
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
    public CandidateDatabaseSet(int capacity = CandidateEvaluationDatabase.DefaultCapacity)
    {
        FirstPass = new CandidateEvaluationDatabase("Candidate Database #1", capacity);
        SecondPass = new CandidateEvaluationDatabase("Candidate Database #2", capacity);
    }

    public CandidateEvaluationDatabase FirstPass { get; }
    public CandidateEvaluationDatabase SecondPass { get; }
}
