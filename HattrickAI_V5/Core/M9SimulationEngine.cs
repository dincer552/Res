using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 Monte Carlo katmanı. Ana M9 rating modelini değiştirmez; aynı rating/chance
/// girdisini farklı venue ve chance-allocation varyantlarıyla tekrar tekrar çalıştırır.
/// Her koşulun sonuçları küçük bir simulation database içinde tutulur ve birleşik
/// dağılımdan en sık skor/sonuç çıkarılır.
/// </summary>
public sealed class M9SimulationEngine
{
    public const int DefaultSimulationCount = 1000;

    private static readonly SimulationScenario[] Scenarios =
    {
        new("Base", 1.00, 1.00, 1.00),
        new("Home+", 1.04, 1.02, 0.98),
        new("Home chance", 1.03, 1.08, 0.96),
        new("Centre chance", 1.02, 1.04, 0.99),
        new("Wing chance", 1.01, 1.02, 1.01),
        new("Low chance", 0.97, 0.94, 1.04),
        new("High chance", 1.04, 1.08, 0.94)
    };

    public M9SimulationResult Simulate(
        M9PredictionResult basePrediction,
        int simulationCount = DefaultSimulationCount,
        int seed = 9051)
    {
        ArgumentNullException.ThrowIfNull(basePrediction);
        simulationCount = Math.Clamp(simulationCount, 100, 10000);

        var rng = new Random(seed);
        var database = new List<M9SimulationRecord>(simulationCount);
        var scenarioResults = new Dictionary<string, M9ScenarioSummary>(StringComparer.Ordinal);
        foreach (var scenario in Scenarios)
            scenarioResults[scenario.Name] = new M9ScenarioSummary(scenario.Name);

        // Her iterasyonda aynı M9 xG çekirdeği, sınırlı venue/chance varyantı ve
        // Poisson gol örneklemesi ile yeniden çalışır. Hard-coded maç sonucu yoktur.
        for (var i = 0; i < simulationCount; i++)
        {
            var scenario = Scenarios[i % Scenarios.Length];
            var ownLambda = Math.Clamp(
                basePrediction.Prediction.ExpectedHomeGoals * scenario.HomeFactor * scenario.ChanceFactor * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);
            var opponentLambda = Math.Clamp(
                basePrediction.Prediction.ExpectedAwayGoals * scenario.DefenceFactor * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);

            var ownGoals = SamplePoisson(rng, ownLambda);
            var opponentGoals = SamplePoisson(rng, opponentLambda);
            var outcome = ownGoals > opponentGoals ? "Galibiyet" : ownGoals == opponentGoals ? "Beraberlik" : "Rakip Galibiyeti";
            var record = new M9SimulationRecord(i + 1, scenario.Name, ownLambda, opponentLambda, ownGoals, opponentGoals, outcome);
            database.Add(record);
            scenarioResults[scenario.Name].Add(record);
        }

        var scoreCounts = database
            .GroupBy(x => $"{x.OwnGoals}-{x.OpponentGoals}", StringComparer.Ordinal)
            .Select(g => new M9ScoreFrequency(g.Key, g.Count(), (double)g.Count() / simulationCount))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Score, StringComparer.Ordinal)
            .ToArray();

        var outcomeCounts = database
            .GroupBy(x => x.Outcome, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var win = Count(outcomeCounts, "Galibiyet");
        var draw = Count(outcomeCounts, "Beraberlik");
        var loss = Count(outcomeCounts, "Rakip Galibiyeti");

        return new M9SimulationResult(
            simulationCount,
            database,
            scoreCounts,
            new M9SimulationOutcome(
                (double)win / simulationCount,
                (double)draw / simulationCount,
                (double)loss / simulationCount),
            scenarioResults.Values.ToArray());
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
}

public sealed record SimulationScenario(string Name, double HomeFactor, double ChanceFactor, double DefenceFactor);

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
    // Ham simulation DB bellekte kalır; API JSON'una 1000 satır taşımıyoruz.
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

public sealed record M9SimulationResult(
    int SimulationCount,
    IReadOnlyList<M9SimulationRecord> Database,
    IReadOnlyList<M9ScoreFrequency> ScoreFrequencies,
    M9SimulationOutcome Outcome,
    IReadOnlyList<M9ScenarioSummary> Scenarios)
{
    // 1000 ham kayıt yalnızca motor içi calibration/debug için tutulur.
    internal IReadOnlyList<M9SimulationRecord> DatabaseInternal => Database;
    public string MostLikelyScore => ScoreFrequencies.FirstOrDefault()?.Score ?? "—";
    public double MostLikelyScoreProbability => ScoreFrequencies.FirstOrDefault()?.Probability ?? 0;
    public string MostLikelyResult
        => Outcome.WinProbability >= Outcome.LossProbability
            ? (Outcome.WinProbability >= Outcome.DrawProbability ? "Galibiyet" : "Beraberlik")
            : (Outcome.LossProbability >= Outcome.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");
}
