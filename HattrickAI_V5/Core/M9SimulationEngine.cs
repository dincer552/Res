using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 event-based Monte Carlo. The paper describes a dynamic 90-minute engine
/// with events occurring in roughly five-minute intervals; this layer samples 18
/// match ticks rather than one whole-match Poisson draw.
/// </summary>
public sealed class M9SimulationEngine
{
    public const int DefaultSimulationCount = 1000;
    public const int MatchTicks = 18;

    private static readonly SimulationScenario[] Scenarios =
    {
        new("Base PDF 36.15/25.65/25.65/12.55", 1.00, 1.00, 1.00),
        new("Sol kanat", 1.00, 1.00, 1.01),
        new("Sağ kanat", 1.00, 1.00, 1.01),
        new("Düşük şans", 0.96, 1.02, 0.99),
        new("Yüksek şans", 1.04, 0.96, 1.02)
    };

    public static M9SimulationResult Simulate(MatchPrediction prediction, int simulationCount = DefaultSimulationCount, int seed = 9051)
    {
        ArgumentNullException.ThrowIfNull(prediction);
        var wrapper = new M9PredictionResult(
            "Unknown", "canonical-match-prediction", prediction, 0.0,
            prediction.PossessionProbability, 1.0 - prediction.PossessionProbability,
            0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5,
            prediction.Location, M9CalibrationStatus.StructuralModelAwaitingHistoricalCalibration)
        {
            EventGoals = prediction.EventGoals
        };
        return new M9SimulationEngine().Simulate(wrapper, simulationCount, seed);
    }

    public M9SimulationResult Simulate(M9PredictionResult basePrediction, int simulationCount = DefaultSimulationCount, int seed = 9051)
    {
        ArgumentNullException.ThrowIfNull(basePrediction);
        simulationCount = Math.Clamp(simulationCount, 100, 10000);

        var rng = new Random(seed);
        var database = new M9SimulationDatabase(simulationCount);
        var scenarioResults = Scenarios.ToDictionary(x => x.Name, x => new M9ScenarioSummary(x.Name), StringComparer.Ordinal);
        var events = basePrediction.EventGoals.Contributions.Count > 0
            ? basePrediction.EventGoals
            : basePrediction.Prediction.EventGoals;

        for (var i = 0; i < simulationCount; i++)
        {
            var scenario = Scenarios[i % Scenarios.Length];
            var homeFactor = basePrediction.Location == MatchLocation.Home ? scenario.HomeFactor : 1.0;
            var ownExpected = Math.Clamp(basePrediction.Prediction.ExpectedHomeGoals * scenario.ChanceVolumeFactor * homeFactor, 0.05, 5.0);
            var opponentExpected = Math.Clamp(basePrediction.Prediction.ExpectedAwayGoals * scenario.OpponentFactor, 0.05, 5.0);

            var ownEventGoalMean = Math.Max(0.0, events.NetSpecialEventGoalContribution * scenario.ChanceVolumeFactor);
            var opponentEventGoalMean = Math.Max(0.0, events.ExpectedGoalsConcededFromOwnGoalEvents * scenario.OpponentFactor);
            var ownNormalMean = Math.Max(0.0, ownExpected - ownEventGoalMean);
            var opponentNormalMean = Math.Max(0.0, opponentExpected - opponentEventGoalMean);

            var ownGoals = 0;
            var opponentGoals = 0;
            for (var tick = 0; tick < MatchTicks; tick++)
            {
                ownGoals += SamplePoisson(rng, ownNormalMean / MatchTicks);
                opponentGoals += SamplePoisson(rng, opponentNormalMean / MatchTicks);
                ownGoals += SampleEventGoalsForTick(rng, events, scenario.ChanceVolumeFactor);
                opponentGoals += SampleOpponentEventGoalsForTick(rng, opponentEventGoalMean);
            }

            var outcome = ownGoals > opponentGoals ? "Galibiyet" : ownGoals == opponentGoals ? "Beraberlik" : "Rakip Galibiyeti";
            var record = new M9SimulationRecord(i + 1, scenario.Name, ownExpected, opponentExpected, ownGoals, opponentGoals, outcome);
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

    private static int SampleEventGoalsForTick(Random rng, M9EventGoalBreakdown events, double multiplier)
    {
        var goals = 0;
        foreach (var contribution in events.Contributions)
        {
            if (contribution.Event == "UnpredictableOwnGoal" || contribution.Event == "PowerfulDefensiveInnerMidfielder") continue;
            var expectedEvents = Math.Max(0.0, contribution.ExpectedEvents * multiplier / MatchTicks);
            if (expectedEvents <= 0 || contribution.GoalProbability <= 0) continue;
            var eventCount = SamplePoisson(rng, expectedEvents);
            for (var i = 0; i < eventCount; i++)
                if (rng.NextDouble() < Math.Clamp(contribution.GoalProbability, 0.0, 1.0)) goals++;
        }
        return goals;
    }

    private static int SampleOpponentEventGoalsForTick(Random rng, double mean)
        => mean <= 0 ? 0 : SamplePoisson(rng, mean / MatchTicks);

    private static int SamplePoisson(Random rng, double lambda)
    {
        if (lambda <= 0) return 0;
        if (lambda > 20.0)
        {
            var normal = lambda + Math.Sqrt(lambda) * BoxMuller(rng);
            return Math.Max(0, (int)Math.Round(normal));
        }
        var threshold = Math.Exp(-lambda);
        var product = 1.0;
        var k = 0;
        do
        {
            k++;
            product *= Math.Max(1e-12, rng.NextDouble());
        } while (product > threshold && k < 100);
        return Math.Max(0, k - 1);
    }

    private static double BoxMuller(Random rng)
    {
        var u1 = Math.Max(1e-12, rng.NextDouble());
        var u2 = Math.Max(1e-12, rng.NextDouble());
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string key)
        => counts.TryGetValue(key, out var value) ? value : 0;
}

public sealed record SimulationScenario(string Name, double ChanceVolumeFactor, double OpponentFactor, double HomeFactor);

public sealed record M9SimulationRecord(int Iteration, string Scenario, double OwnExpectedGoals, double OpponentExpectedGoals, int OwnGoals, int OpponentGoals, string Outcome);
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
        .FirstOrDefault()?.Key ?? "0-0";
    public void Add(M9SimulationRecord record) => _records.Add(record);
}

public sealed class M9SimulationDatabase
{
    private readonly List<M9SimulationRecord> _records;
    public M9SimulationDatabase(int capacity) => _records = new List<M9SimulationRecord>(capacity);
    public IReadOnlyList<M9SimulationRecord> Records => _records;
    public void Add(M9SimulationRecord record) => _records.Add(record);
}

public sealed record M9SimulationResult(
    int SimulationCount,
    IReadOnlyList<M9ScoreFrequency> ScoreFrequencies,
    M9SimulationOutcome Outcome,
    IReadOnlyList<M9ScenarioSummary> Scenarios,
    M9SimulationDatabase Database)
{
    public string MostLikelyScore => ScoreFrequencies.FirstOrDefault()?.Score ?? "0-0";
    public double MostLikelyScoreProbability => ScoreFrequencies.FirstOrDefault()?.Probability ?? 0.0;
    public string MostLikelyResult => Outcome.WinProbability >= Outcome.LossProbability
        ? (Outcome.WinProbability >= Outcome.DrawProbability ? "Galibiyet" : "Beraberlik")
        : (Outcome.LossProbability >= Outcome.DrawProbability ? "Rakip Galibiyeti" : "Beraberlik");
}
