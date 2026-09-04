using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

public sealed record HistoricalCalibrationSample(
    string MatchId,
    int HatStatsHome,
    int HatStatsAway,
    double OwnMidfieldRating,
    double OpponentMidfieldRating,
    int OwnNormalLeft,
    int OwnNormalCentre,
    int OwnNormalRight,
    int OpponentNormalLeft,
    int OpponentNormalCentre,
    int OpponentNormalRight,
    string OwnTactic,
    double OwnTacticRating,
    int LongShotAttempts,
    int LongShotGoals,
    IReadOnlyDictionary<string, int>? EventCounts = null,
    IReadOnlyDictionary<string, int>? EventGoals = null);

public sealed record HistoricalEventCalibration(
    string EventName,
    double EventsPerMatch,
    double GoalRate,
    int MatchesWithEvent,
    int TotalEvents,
    int TotalGoals);

public sealed record HistoricalLongShotCalibration(
    int EligibleMatches,
    int LongShotAttempts,
    int LongShotGoals,
    double ConversionRate,
    double GoalRate,
    double MeanAbsoluteCurveError,
    IReadOnlyDictionary<int, double> ObservedConversionByTacticRating,
    bool SufficientForProduction);

public sealed record HistoricalCalibrationReport(
    string Source,
    int InputMatches,
    int EligibleMatches,
    int MinimumHatStats,
    double MeanRegularSectorChances,
    double RegularSectorChanceSignedErrorVsPaper,
    double LeftShare,
    double CentreShare,
    double RightShare,
    double LeftShareErrorVsPaper,
    double CentreShareErrorVsPaper,
    double RightShareErrorVsPaper,
    IReadOnlyDictionary<string, HistoricalEventCalibration> Events,
    HistoricalLongShotCalibration LongShots,
    bool ProductionEligible,
    string ProductionDecision);

/// <summary>
/// Fits historical CHPP observations against the documented paper baseline.
/// It is deliberately analysis-first: no small fixture may silently replace
/// the production equations. A real corpus must pass the activation gate.
/// </summary>
public static class HistoricalCalibrationEngine
{
    public const int DefaultMinimumHatStats = 333;
    public const int MinimumMatchesForProduction = 250;
    public const int MinimumLongShotAttemptsForProduction = 250;

    private static readonly IReadOnlyDictionary<string, (double EventRate, double GoalRate)> PaperEvents =
        new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Winger"] = (0.2163, 0.4951),
            ["TechnicalOverHead"] = (0.1277, 0.2937),
            ["QuickRush"] = (0.1286, 0.3670),
            ["QuickPass"] = (0.1219, 0.4387),
            ["UnpredictableLongPass"] = (0.0687, 0.4090),
            ["UnpredictableScoreOwn"] = (0.0536, 0.5822),
            ["UnpredictableSpecialAction"] = (0.0560, 0.4241),
            ["UnpredictableMistake"] = (0.0290, 0.1816),
            ["UnpredictableOwnGoal"] = (0.0392, 0.1725),
            ["ExperiencedForward"] = (0.0400, 0.3704),
            ["InexperiencedDefender"] = (0.0392, 0.1050),
            ["TiredDefender"] = (0.0004, 0.3432),
            ["Corner"] = (0.2922, 0.4849)
        };

    /// <summary>Published 2026 study baseline: 1M competitive matches.</summary>
    public static IReadOnlyDictionary<string, (double EventRate, double GoalRate)> PaperEventReference => PaperEvents;

    public static HistoricalCalibrationReport Analyze(
        IEnumerable<HistoricalCalibrationSample> samples,
        int minimumHatStats = DefaultMinimumHatStats,
        int minimumMatchesForProduction = MinimumMatchesForProduction,
        int minimumLongShotAttemptsForProduction = MinimumLongShotAttemptsForProduction)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var input = samples.Where(s => !string.IsNullOrWhiteSpace(s.MatchId)).ToArray();
        var eligible = input
            .Where(s => s.HatStatsHome >= minimumHatStats && s.HatStatsAway >= minimumHatStats)
            .ToArray();

        if (eligible.Length == 0)
            return Empty(input.Length, minimumHatStats);

        var ownLeft = eligible.Sum(s => Math.Max(0, s.OwnNormalLeft));
        var ownCentre = eligible.Sum(s => Math.Max(0, s.OwnNormalCentre));
        var ownRight = eligible.Sum(s => Math.Max(0, s.OwnNormalRight));
        var oppLeft = eligible.Sum(s => Math.Max(0, s.OpponentNormalLeft));
        var oppCentre = eligible.Sum(s => Math.Max(0, s.OpponentNormalCentre));
        var oppRight = eligible.Sum(s => Math.Max(0, s.OpponentNormalRight));
        var regularTotal = ownLeft + ownCentre + ownRight + oppLeft + oppCentre + oppRight;
        var meanRegular = (double)regularTotal / eligible.Length;
        var paperRegular = M8ChanceAllocationEngine.PaperExpectedRegularSectorChances;
        var sectorTotal = regularTotal;

        var events = AnalyzeEvents(eligible);
        var longShots = AnalyzeLongShots(eligible);
        var eligibleForActivation =
            eligible.Length >= minimumMatchesForProduction &&
            longShots.SufficientForProduction;

        return new HistoricalCalibrationReport(
            "real-CHPP-corpus",
            input.Length,
            eligible.Length,
            minimumHatStats,
            meanRegular,
            meanRegular - paperRegular,
            sectorTotal == 0 ? 0 : (double)(ownLeft + oppLeft) / sectorTotal,
            sectorTotal == 0 ? 0 : (double)(ownCentre + oppCentre) / sectorTotal,
            sectorTotal == 0 ? 0 : (double)(ownRight + oppRight) / sectorTotal,
            sectorTotal == 0 ? 0 : (double)(ownLeft + oppLeft) / sectorTotal - M8ChanceAllocationEngine.PaperLeftAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            sectorTotal == 0 ? 0 : (double)(ownCentre + oppCentre) / sectorTotal - M8ChanceAllocationEngine.PaperCentreAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            sectorTotal == 0 ? 0 : (double)(ownRight + oppRight) / sectorTotal - M8ChanceAllocationEngine.PaperRightAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            events,
            longShots,
            eligibleForActivation,
            eligibleForActivation
                ? "Candidate calibration accepted for review; activation still requires regression comparison against the paper baseline."
                : "Validation only; production remains on the paper baseline because the historical corpus or Long Shot coverage is below the activation gate.");
    }

    private static IReadOnlyDictionary<string, HistoricalEventCalibration> AnalyzeEvents(IReadOnlyList<HistoricalCalibrationSample> samples)
    {
        var result = new Dictionary<string, HistoricalEventCalibration>(StringComparer.OrdinalIgnoreCase);
        foreach (var eventName in PaperEvents.Keys)
        {
            var totalEvents = samples.Sum(s => TryGet(s.EventCounts, eventName));
            var totalGoals = samples.Sum(s => TryGet(s.EventGoals, eventName));
            var matchesWithEvent = samples.Count(s => TryGet(s.EventCounts, eventName) > 0);
            result[eventName] = new HistoricalEventCalibration(
                eventName,
                (double)totalEvents / samples.Count,
                totalEvents == 0 ? 0.0 : Math.Clamp((double)totalGoals / totalEvents, 0.0, 1.0),
                matchesWithEvent,
                totalEvents,
                totalGoals);
        }
        return result;
    }

    private static HistoricalLongShotCalibration AnalyzeLongShots(IReadOnlyList<HistoricalCalibrationSample> samples)
    {
        var longShotSamples = samples
            .Where(s => s.LongShotAttempts > 0 || s.OwnTactic.Equals("LongShots", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var attempts = longShotSamples.Sum(s => Math.Max(0, s.LongShotAttempts));
        var goals = longShotSamples.Sum(s => Math.Clamp(s.LongShotGoals, 0, Math.Max(0, s.LongShotAttempts)));
        var lmr = longShotSamples.Sum(s => Math.Max(0, s.OwnNormalLeft) + Math.Max(0, s.OwnNormalCentre) + Math.Max(0, s.OwnNormalRight));
        var conversion = lmr == 0 ? 0.0 : (double)attempts / lmr;
        var goalRate = attempts == 0 ? 0.0 : (double)goals / attempts;

        var observedByRating = longShotSamples
            .GroupBy(s => Math.Clamp((int)Math.Round(s.OwnTacticRating), 0, 30))
            .ToDictionary(
                g => g.Key,
                g => {
                    var a = g.Sum(s => Math.Max(0, s.LongShotAttempts));
                    var baseAttempts = g.Sum(s => Math.Max(0, s.OwnNormalLeft) + Math.Max(0, s.OwnNormalCentre) + Math.Max(0, s.OwnNormalRight));
                    return baseAttempts == 0 ? 0.0 : (double)a / baseAttempts;
                });

        var mae = observedByRating.Count == 0
            ? 0.0
            : observedByRating.Average(kvp => Math.Abs(kvp.Value - M8ChanceAllocationEngine.CalculateTacticConversionRate(AdvancedTactic.LongShots, kvp.Key)));

        return new HistoricalLongShotCalibration(
            longShotSamples.Length,
            attempts,
            goals,
            conversion,
            goalRate,
            mae,
            observedByRating,
            longShotSamples.Length > 0 && attempts >= MinimumLongShotAttemptsForProduction);
    }

    private static int TryGet(IReadOnlyDictionary<string, int>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? Math.Max(0, value) : 0;

    private static HistoricalCalibrationReport Empty(int inputMatches, int minimumHatStats)
        => new(
            "real-CHPP-corpus",
            inputMatches,
            0,
            minimumHatStats,
            0.0,
            -M8ChanceAllocationEngine.PaperExpectedRegularSectorChances,
            0.0,
            0.0,
            0.0,
            -M8ChanceAllocationEngine.PaperLeftAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            -M8ChanceAllocationEngine.PaperCentreAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            -M8ChanceAllocationEngine.PaperRightAttackShare / M8ChanceAllocationEngine.PaperRegularSectorShare,
            new Dictionary<string, HistoricalEventCalibration>(StringComparer.OrdinalIgnoreCase),
            new HistoricalLongShotCalibration(0, 0, 0, 0.0, 0.0, 0.0, new Dictionary<int, double>(), false),
            false,
            "Validation only; no eligible historical corpus was supplied.");
}
