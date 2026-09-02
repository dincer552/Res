using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 Monte Carlo katmanı. Ana rating çekirdeğini değiştirmez; M9'un ürettiği
/// sektör kalitesini farklı chance-allocation kombinasyonları ve venue varyantları
/// ile tekrar tekrar örnekler. Ham kayıtlar calibration/debug için tutulur,
/// API'ye yalnızca özet dağılım çıkar.
/// </summary>
public sealed class M9SimulationEngine
{
    public const int DefaultSimulationCount = 1000;

    // Hattrick temel regular chance dağılımı: merkez %35, kanatlar %25/%25,
    // set-piece %15. Varyantlar bu dağılımın etrafında kontrollü oynar.
    private static readonly SimulationScenario[] Scenarios =
    {
        new("Base 35/25/25/15", 1.00, 0.35, 0.25, 0.25, 0.15, 1.00),
        new("Sol kanat", 1.00, 0.30, 0.38, 0.17, 0.15, 1.00),
        new("Sağ kanat", 1.00, 0.30, 0.17, 0.38, 0.15, 1.00),
        new("Merkez", 1.00, 0.50, 0.25, 0.10, 0.15, 1.00),
        new("Dengeli hücum", 1.00, 0.34, 0.33, 0.18, 0.15, 1.00),
        new("Düşük şans", 0.96, 0.35, 0.25, 0.25, 0.15, 1.02),
        new("Yüksek şans", 1.04, 0.35, 0.25, 0.25, 0.15, 0.96)
    };

    public M9SimulationResult Simulate(M9PredictionResult basePrediction, int simulationCount = DefaultSimulationCount, int seed = 9051)
    {
        ArgumentNullException.ThrowIfNull(basePrediction);
        simulationCount = Math.Clamp(simulationCount, 100, 10000);

        var rng = new Random(seed);
        var database = new M9SimulationDatabase(simulationCount);
        var scenarioResults = Scenarios.ToDictionary(x => x.Name, x => new M9ScenarioSummary(x.Name), StringComparer.Ordinal);
        var isHome = basePrediction.Location.ToString().Contains("Home", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < simulationCount; i++)
        {
            var scenario = Scenarios[i % Scenarios.Length];
            var venueFactor = isHome ? scenario.HomeFactor : 1.0;

            var ownQuality = WeightedSectorQuality(basePrediction, scenario);
            var opponentQuality = WeightedOpponentQuality(basePrediction, scenario);

            var ownLambda = Math.Clamp(
                basePrediction.Prediction.ExpectedHomeGoals * venueFactor * scenario.ChanceVolumeFactor *
                QualityAdjustment(ownQuality, basePrediction.OwnAttackQuality) * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);
            var opponentLambda = Math.Clamp(
                basePrediction.Prediction.ExpectedAwayGoals * scenario.OpponentFactor *
                QualityAdjustment(opponentQuality, basePrediction.OpponentAttackQuality) * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);

            var ownGoals = SamplePoisson(rng, ownLambda);
            var opponentGoals = SamplePoisson(rng, opponentLambda);
            var outcome = ownGoals > opponentGoals ? "Galibiyet" : ownGoals == opponentGoals ? "Beraberlik" : "Rakip Galibiyeti";
            var record = new M9SimulationRecord(i + 1, scenario.Name, ownLambda, opponentLambda, ownGoals, opponentGoals, outcome);
            database.Add(record);
            scenarioResults[scenario.Name].Add(record);
        }

        var scoreCounts = database.Records
            .GroupBy(x => $"{x.OwnGoals}-{x.OpponentGoals}", StringComparer.Ordinal)
            .Select(g => new M9ScoreFrequency(g.Key, g.Count(), (double)g.Count() / simulationCount))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Score, StringComparer.Ordinal)
            .ToArray();

        var outcomeCounts = database.Records
            .GroupBy(x => x.Outcome, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var win = Count(outcomeCounts, "Galibiyet");
        var draw = Count(outcomeCounts, "Beraberlik");
        var loss = Count(outcomeCounts, "Rakip Galibiyeti");

        return new M9SimulationResult(
            simulationCount,
            scoreCounts,
            new M9SimulationOutcome((double)win / simulationCount, (double)draw / simulationCount, (double)loss / simulationCount),
            scenarioResults.Values.ToArray(),
            database);
    }

    private static double WeightedSectorQuality(M9PredictionResult p, SimulationScenario s)
        => Clamp01((p.OwnLeftAttackVsRightDefence * s.LeftWeight) +
                   (p.OwnCentreAttackVsCentreDefence * s.CentreWeight) +
                   (p.OwnRightAttackVsLeftDefence * s.RightWeight) +
                   (p.OwnAttackQuality * s.SetPieceWeight));

    private static double WeightedOpponentQuality(M9PredictionResult p, SimulationScenario s)
        => Clamp01((p.OpponentLeftAttackVsOwnRightDefence * s.RightWeight) +
                   (p.OpponentCentreAttackVsOwnCentreDefence * s.CentreWeight) +
                   (p.OpponentRightAttackVsOwnLeftDefence * s.LeftWeight) +
                   (p.OpponentAttackQuality * s.SetPieceWeight));

    private static double QualityAdjustment(double scenarioQuality, double baseQuality)
    {
        var safeBase = Math.Max(0.05, baseQuality);
        return Math.Clamp(0.94 + (0.12 * (scenarioQuality / safeBase)), 0.85, 1.15);
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string key)
        => counts.TryGetValue(key, out var value) ? value : 0;

    private static double RandomFactor(Random rng, double min, double max)
        => min + (rng.NextDouble() * (max - min));

    private static int SamplePoisson(Random rng, double lambda)
    {
        var threshold = Math.Exp(-lambda);
        var product = 1.0;
        var k = 0;
        do
        {
            k++;
            product *= Math.Max(1e-12, rng.NextDouble());
        } while (product > threshold && k < 20);
        return Math.Max(0, k - 1);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

public sealed record SimulationScenario(
    string Name,
    double ChanceVolumeFactor,
    double CentreWeight,
    double LeftWeight,
    double RightWeight,
    double SetPieceWeight,
    double OpponentFactor)
{
    // Normal maçta ev sahibi etkisi M7 ratingine zaten girer; Monte Carlo'da
    // yalnızca senaryo varyasyonunu temsil eden küçük ek duyarlılık kullanılır.
    public double HomeFactor => Name == "Yüksek şans" ? 1.02 : Name == "Düşük şans" ? 0.99 : 1.01;
}

public sealed record M9SimulationRecord(
    int Iteration,
    string Scenario,
    double OwnExpectedGoals,
    double OpponentExpectedGoals,
    int OwnGoals,
    int OpponentGoals,
    string Outcome);

public sealed record M9ScoreFrequency(string Score, int Count, double Probability);
public sealed record M9SimulationOutcome(double WinProbability, double DrawProbability, double LossProbability);

public sealed class M9ScenarioSummary
{
    private readonly List<M9SimulationRecord> _records = new();
    public M9ScenarioSummary(string scenario) => Scenario = scenario;
    public string Scenario { get; }
    internal IReadOnlyList<M9SimulationRecord> Records => _records;
    public int Count => _records.Count;
    public string MostLikelyScore => _records
        .GroupBy(x => $"{x.OwnGoals}-{x.OpponentGoals}", StringComparer.Ordinal)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key, StringComparer.Ordinal)
        .Select(g => g.Key)
        .FirstOrDefault() ?? "—";
    public void Add(M9SimulationRecord record) => _records.Add(record);
}

internal sealed class M9SimulationDatabase
{
    private readonly List<M9SimulationRecord> _records;
    public M9SimulationDatabase(int capacity) => _records = new List<M9SimulationRecord>(capacity);
    public IReadOnlyList<M9SimulationRecord> Records => _records;
    public void Add(M9SimulationRecord record) => _records.Add(record);
}

public sealed class M9SimulationResult
{
    internal M9SimulationResult(
        int simulationCount,
        IReadOnlyList<M9ScoreFrequency> scoreFrequencies,
        M9SimulationOutcome outcome,
        IReadOnlyList<M9ScenarioSummary> scenarios,
        M9SimulationDatabase database)
    {
        SimulationCount = simulationCount;
        ScoreFrequencies = scoreFrequencies;
        Outcome = outcome;
        Scenarios = scenarios;
        Database = database;
    }

    public int SimulationCount { get; }
    public IReadOnlyList<M9ScoreFrequency> ScoreFrequencies { get; }
    public M9SimulationOutcome Outcome { get; }
    public IReadOnlyList<M9ScenarioSummary> Scenarios { get; }
    internal M9SimulationDatabase Database { get; }
    public string MostLikelyScore => ScoreFrequencies.FirstOrDefault()?.Score ?? "—";
    public double MostLikelyScoreProbability => ScoreFrequencies.FirstOrDefault()?.Probability ?? 0;
    public string MostLikelyResult
        => Outcome.WinProbability >= Outcome.LossProbability
            ? (Outcome.WinProbability >= Outcome.DrawProbability ? "Galibiyet" : "Beraberlik")
            : (Outcome.LossProbability >= Outcome.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");
}
