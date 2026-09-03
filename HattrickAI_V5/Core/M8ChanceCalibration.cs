using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Historical calibration layer for M8. Production mechanisms are sourced
/// from the 2026 Hattrick research paper; this analyzer measures observed CHPP
/// sector data against that baseline before any further coefficient changes.
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

public sealed record M8SectorCalibrationReport(
    int SampleCount,
    int ObservedLeft,
    int ObservedCentre,
    int ObservedRight,
    int ObservedTotal,
    double ObservedLeftShare,
    double ObservedCentreShare,
    double ObservedRightShare,
    double PdfLeftShare,
    double PdfCentreShare,
    double PdfRightShare,
    double LeftShareError,
    double CentreShareError,
    double RightShareError,
    IReadOnlyDictionary<string, M8SectorCalibrationReport> ByTactic);

public sealed record M8ChanceCalibrationReport(
    int SampleCount,
    double MeanAbsoluteTotalChanceError,
    double MeanSignedTotalChanceError,
    double MeanAbsoluteOwnChanceError,
    double MeanAbsoluteOpponentChanceError,
    double MeanAbsoluteOwnershipError,
    IReadOnlyList<M8ChanceCalibrationRow> Rows,
    M8SectorCalibrationReport SectorCalibration);

public static class M8ChanceCalibrationAnalyzer
{
    public static M8ChanceCalibrationReport Analyze(IEnumerable<M8HistoricalChanceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var materialized = samples.ToArray();
        var rows = materialized.Select(sample =>
        {
            // Calibration of chance ownership deliberately uses the same PDF
            // Eq.1/2 production mechanism as M8.
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

        var sector = AnalyzeSectors(materialized);
        if (rows.Length == 0)
            return new M8ChanceCalibrationReport(0, 0, 0, 0, 0, 0, rows, sector);

        return new M8ChanceCalibrationReport(
            rows.Length,
            rows.Average(x => Math.Abs(x.TotalChanceError)),
            rows.Average(x => x.TotalChanceError),
            rows.Average(x => Math.Abs(x.OwnChanceError)),
            rows.Average(x => Math.Abs(x.OpponentChanceError)),
            rows.Average(x => Math.Abs(x.OwnOwnershipError)),
            rows,
            sector);
    }

    public static M8SectorCalibrationReport AnalyzeSectors(IEnumerable<M8HistoricalChanceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var materialized = samples
            .Where(x => x.ObservedOwnLeftChances.HasValue || x.ObservedOwnCentreChances.HasValue || x.ObservedOwnRightChances.HasValue)
            .ToArray();
        var left = materialized.Sum(x => x.ObservedOwnLeftChances ?? 0);
        var centre = materialized.Sum(x => x.ObservedOwnCentreChances ?? 0);
        var right = materialized.Sum(x => x.ObservedOwnRightChances ?? 0);
        var total = left + centre + right;
        var leftShare = total == 0 ? 0 : (double)left / total;
        var centreShare = total == 0 ? 0 : (double)centre / total;
        var rightShare = total == 0 ? 0 : (double)right / total;

        var grouped = materialized
            .GroupBy(x => string.IsNullOrWhiteSpace(x.OwnTactic) ? "Normal" : x.OwnTactic!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => AnalyzeSectorsSingle(g), StringComparer.OrdinalIgnoreCase);

        return new M8SectorCalibrationReport(
            materialized.Length, left, centre, right, total,
            leftShare, centreShare, rightShare,
            M8ChanceAllocationEngine.PaperLeftAttackShare,
            M8ChanceAllocationEngine.PaperCentreAttackShare,
            M8ChanceAllocationEngine.PaperRightAttackShare,
            leftShare - M8ChanceAllocationEngine.PaperLeftAttackShare,
            centreShare - M8ChanceAllocationEngine.PaperCentreAttackShare,
            rightShare - M8ChanceAllocationEngine.PaperRightAttackShare,
            grouped);
    }

    private static M8SectorCalibrationReport AnalyzeSectorsSingle(IEnumerable<M8HistoricalChanceSample> samples)
    {
        var a = samples.ToArray();
        var left = a.Sum(x => x.ObservedOwnLeftChances ?? 0);
        var centre = a.Sum(x => x.ObservedOwnCentreChances ?? 0);
        var right = a.Sum(x => x.ObservedOwnRightChances ?? 0);
        var total = left + centre + right;
        var l = total == 0 ? 0 : (double)left / total;
        var c = total == 0 ? 0 : (double)centre / total;
        var r = total == 0 ? 0 : (double)right / total;
        return new M8SectorCalibrationReport(
            a.Length, left, centre, right, total, l, c, r,
            M8ChanceAllocationEngine.PaperLeftAttackShare,
            M8ChanceAllocationEngine.PaperCentreAttackShare,
            M8ChanceAllocationEngine.PaperRightAttackShare,
            l - M8ChanceAllocationEngine.PaperLeftAttackShare,
            c - M8ChanceAllocationEngine.PaperCentreAttackShare,
            r - M8ChanceAllocationEngine.PaperRightAttackShare,
            new Dictionary<string, M8SectorCalibrationReport>(StringComparer.OrdinalIgnoreCase));
    }
}
