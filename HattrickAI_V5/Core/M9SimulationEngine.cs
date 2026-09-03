using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 event-based Monte Carlo layer. M8 owns chance volume; M9 samples normal
/// scoring and documented event goals separately so special-event expectation is
/// not silently folded into a single Poisson lambda.
/// </summary>
public sealed class M9SimulationEngine
{
    public const int DefaultSimulationCount = 1000;

    // PDF Eq. 3: Left 25.65%, Centre 36.15%, Right 25.65%, set-pieces 12.55%.
    private static readonly SimulationScenario[] Scenarios =
    {
        new("Base PDF 36.15/25.65/25.65/12.55", 1.00, 0.3615, 0.2565, 0.2565, 0.1255, 1.00),
        new("Sol kanat", 1.00, 0.30, 0.42, 0.155, 0.1255, 1.00),
        new("Sağ kanat", 1.00, 0.30, 0.155, 0.42, 0.1255, 1.00),
        new("Merkez", 1.00, 0.50, 0.23725, 0.13725, 0.1255, 1.00),
        new("Dengeli hücum", 1.00, 0.34, 0.32, 0.215, 0.1255, 1.00),
        new("Düşük şans", 0.96, 0.3615, 0.2565, 0.2565, 0.1255, 1.02),
        new("Yüksek şans", 1.04, 0.3615, 0.2565, 0.2565, 0.1255, 0.96)
    };

    public M9SimulationResult Simulate(M9PredictionResult basePrediction, int simulationCount = DefaultSimulationCount, int seed = 9051)
    {
        ArgumentNullException.ThrowIfNull(basePrediction);
        simulationCount = Math.Clamp(simulationCount, 100, 10000);

        var rng = new Random(seed);
        var database = new M9SimulationDatabase(simulationCount);
        var scenarioResults = Scenarios.ToDictionary(x => x.Name, x => new M9ScenarioSummary(x.Name), StringComparer.Ordinal);

        for (var i = 0; i < simulationCount; i++)
        {
            var scenario = Scenarios[i % Scenarios.Length];
            var venueFactor = basePrediction.Location == MatchLocation.Home ? scenario.HomeFactor : 1.0;

            var ownQuality = WeightedSectorQuality(basePrediction, scenario);
            var opponentQuality = WeightedOpponentQuality(basePrediction, scenario);

            var ownSpecialGoalMean = Math.Max(0.0, basePrediction.EventGoals.NetSpecialEventGoalContribution);
            var ownSpecialGoalMeanForSimulation = Math.Max(0.0, ownSpecialGoalMean * scenario.ChanceVolumeFactor);
            var opponentSpecialGoalMean = Math.Max(0.0, basePrediction.EventGoals.ExpectedGoalsConcededFromOwnGoalEvents * scenario.OpponentFactor);

            var totalOwnExpected = Math.Clamp(
                basePrediction.Prediction.ExpectedHomeGoals * venueFactor * scenario.ChanceVolumeFactor *
                QualityAdjustment(ownQuality, basePrediction.OwnAttackQuality) * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);
            var totalOpponentExpected = Math.Clamp(
                basePrediction.Prediction.ExpectedAwayGoals * scenario.OpponentFactor *
                QualityAdjustment(opponentQuality, basePrediction.OpponentAttackQuality) * RandomFactor(rng, 0.94, 1.06),
                0.05, 5.0);

            // Decompose the pre-existing M9 expectation into normal and event
            // components. The event component is sampled independently below.
            var ownNormalMean = Math.Max(0.0, totalOwnExpected - ownSpecialGoalMeanForSimulation);
            var opponentNormalMean = Math.Max(0.0, totalOpponentExpected - opponentSpecialGoalMean);

            var ownNormalGoals = SamplePoisson(rng, ownNormalMean);
            var opponentNormalGoals = SamplePoisson(rng, opponentNormalMean);
            var ownEventGoals = SampleEventGoals(rng, basePrediction.EventGoals, ownSpecialGoalMeanForSimulation);
            var opponentEventGoals = SampleOpponentEventGoals(rng, opponentSpecialGoalMean);

            var ownGoals = ownNormalGoals + ownEventGoals;
            var opponentGoals = opponentNormalGoals + opponentEventGoals;
            var outcome = ownGoals > opponentGoals ? "Galibiyet" : ownGoals == opponentGoals ? "Beraberlik" : "Rakip Galibiyeti";
            var record = new M9SimulationRecord(i + 1, scenario.Name, totalOwnExpected, totalOpponentExpected, ownGoals, opponentGoals, outcome);
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

    private static int SampleEventGoals(Random rng, M9EventGoalBreakdown events, double specialGoalMean)
    {
        if (specialGoalMean <= 0) return 0;

        // Each event class has an expected event count and a documented goal
        // conversion rate. Sample event occurrence first, then resolve goal/no-goal.
        var goals = 0;
        foreach (var contribution in events.Contributions)
        {
            if (contribution.ExpectedEvents <= 0 || contribution.GoalProbability <= 0) continue;
            var eventCount = SamplePoisson(rng, contribution.ExpectedEvents);
            for (var i = 0; i < eventCount; i++)
                if (rng.NextDouble() < contribution.GoalProbability) goals++;
        }

        // If the event list is incomplete, preserve its calibrated aggregate mean
        // rather than creating an artificial extra goal source. The current engine
        // exposes all active documented classes, so this should normally be zero.
        return goals;
    }

    private static int SampleOpponentEventGoals(Random rng, double mean)
        => mean <= 0 ? 0 : SamplePoisson(rng, mean);

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
        if (lambda <= 0) return 0;
        var threshold = Math.Exp(-lambda);
        var product = 1.0;
        var k = 0;
        do
        {
            k++;
            product *= Math.Max(1e-12, rng.NextDouble());
        } while (product > threshold && k < 50);
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
