using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// PHASE D calibration data for M8 normal-chance volume.
/// This layer is intentionally observation-only: it measures the current
/// structural M8 output against real historical matches and does not change
/// production coefficients.
/// </summary>
public sealed record M8HistoricalChanceSample(
    string MatchId,
    bool IsHome,
    double MidfieldShare,
    int ObservedTotalRegularChances,
    int ObservedOwnRegularChances,
    int ObservedOpponentRegularChances,
    int? ObservedOwnLeftChances = null,
    int? ObservedOwnCentreChances = null,
    int? ObservedOwnRightChances = null,
    int? ObservedOwnSetPieceChances = null,
    string? OwnTactic = null,
    string? OpponentTactic = null,
    string? OwnFormation = null,
    string? OpponentFormation = null,
    int? OwnGoals = null,
    int? OpponentGoals = null);

public sealed record M8ChanceCalibrationRow(
    string MatchId,
    double MidfieldShare,
    double PredictedOwnRegularChances,
    double PredictedOpponentRegularChances,
    int ObservedOwnRegularChances,
    int ObservedOpponentRegularChances,
    double OwnChanceError,
    double OpponentChanceError,
    double TotalChanceError,
    double OwnOwnershipError);

public sealed record M8ChanceCalibrationReport(
    int SampleCount,
    double MeanAbsoluteTotalChanceError,
    double MeanSignedTotalChanceError,
    double MeanAbsoluteOwnChanceError,
    double MeanAbsoluteOpponentChanceError,
    double MeanAbsoluteOwnershipError,
    IReadOnlyList<M8ChanceCalibrationRow> Rows);

public static class M8ChanceCalibrationAnalyzer
{
    public static M8ChanceCalibrationReport Analyze(IEnumerable<M8HistoricalChanceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var rows = samples.Select(sample =>
        {
            var allocation = M8ChanceAllocationEngine.Calculate(sample.MidfieldShare);
            var predictedOwn = allocation.OwnRegularChanceExpected;
            var predictedOpponent = allocation.OpponentRegularChanceExpected;
            var observedTotal = sample.ObservedTotalRegularChances;
            var predictedTotal = predictedOwn + predictedOpponent;
            var observedOwnOwnership = observedTotal <= 0
                ? 0.5
                : Math.Clamp((double)sample.ObservedOwnRegularChances / observedTotal, 0.0, 1.0);
            var predictedOwnOwnership = predictedTotal <= 0
                ? 0.5
                : Math.Clamp(predictedOwn / predictedTotal, 0.0, 1.0);

            return new M8ChanceCalibrationRow(
                sample.MatchId,
                Math.Clamp(sample.MidfieldShare, 0.0, 1.0),
                predictedOwn,
                predictedOpponent,
                sample.ObservedOwnRegularChances,
                sample.ObservedOpponentRegularChances,
                predictedOwn - sample.ObservedOwnRegularChances,
                predictedOpponent - sample.ObservedOpponentRegularChances,
                predictedTotal - observedTotal,
                predictedOwnOwnership - observedOwnOwnership);
        }).ToArray();

        if (rows.Length == 0)
        {
            return new M8ChanceCalibrationReport(0, 0, 0, 0, 0, 0, rows);
        }

        return new M8ChanceCalibrationReport(
            rows.Length,
            rows.Average(x => Math.Abs(x.TotalChanceError)),
            rows.Average(x => x.TotalChanceError),
            rows.Average(x => Math.Abs(x.OwnChanceError)),
            rows.Average(x => Math.Abs(x.OpponentChanceError)),
            rows.Average(x => Math.Abs(x.OwnOwnershipError)),
            rows);
    }
}
