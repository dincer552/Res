using HattrickAI.V5.Core;
using HattrickAI.V5.OfflineTests;

var startFrom = args.Length > 0 && args[0].StartsWith("c", StringComparison.OrdinalIgnoreCase) ? args[0].ToLowerInvariant() : "c1";
var path = args.Length > 1 ? args[1] : args.Length > 0 && !args[0].StartsWith("c", StringComparison.OrdinalIgnoreCase) ? args[0] : "TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json";
var startNumber = startFrom.Length > 1 && int.TryParse(startFrom[1..], out var parsed) ? parsed : 1;
if (startNumber < 1 || startNumber > 18) throw new ArgumentException($"Geçersiz acceptance başlangıcı: {startFrom}. c1-c18 kullanın.");

bool From(int c) => startNumber <= c;

if (From(1))
{
    var r = WebInputIntegrityRegression.Run(); if (r != 0) return r;
    r = CoreWebParityRegression.Run(); if (r != 0) return r;
    r = await M3M11EndToEndRegression.RunAsync(path); if (r != 0) return r;
}
if (From(2)) { var r = M4LegalFormationRegression.Run(); if (r != 0) return r; }
if (From(3))
{
    var r = HistoricalCalibrationRegression.Run(); if (r != 0) return r;
    r = SetPieceTakerCalibrationRegression.Run(); if (r != 0) return r;
    r = SpecialtyInteractionRegression.Run(); if (r != 0) return r;
    r = TacticPaperMappingRegression.Run(); if (r != 0) return r;
    r = LongShotOpportunityRegression.Run(); if (r != 0) return r;
    r = M9EventGoalRegression.Run(); if (r != 0) return r;
    r = await M5XICandidatesRegression.RunAsync(path); if (r != 0) return r;
}
if (From(4)) { var r = await M6ACandidateEvaluationRegression.RunAsync(path); if (r != 0) return r; }
if (From(5)) { var r = await M7RegionalRatingRegression.RunAsync(path); if (r != 0) return r; }
if (From(6)) { var r = await M7_2TacticalScenarioRegression.RunAsync(path); if (r != 0) return r; }
if (From(7)) { var r = await M8ChanceModelRegression.RunAsync(path); if (r != 0) return r; }
if (From(8)) { var r = await M9PredictionRegression.RunAsync(path); if (r != 0) return r; }
if (From(9)) { var r = await DB1FormationCoverageRegression.RunAsync(path); if (r != 0) return r; }
if (From(10)) { var r = await M10FormationCompetitionRegression.RunAsync(path); if (r != 0) return r; }
if (From(11)) { var r = await M10ToM6BRankDrivenHandoffRegression.RunAsync(path); if (r != 0) return r; }
if (From(12)) { var r = await M6BRefinementRegression.RunAsync(path); if (r != 0) return r; }
if (From(13)) { var r = await DB2FormationCoverageRegression.RunAsync(path); if (r != 0) return r; }
if (From(14)) { var r = await M11FinalistPoolRegression.RunAsync(path); if (r != 0) return r; }
if (From(15)) { var r = await M11FinalSelectionRegression.RunAsync(path); if (r != 0) return r; }
if (From(16)) { var r = await FinalPlanContinuityRegression.RunAsync(path); if (r != 0) return r; }
if (From(17)) { var r = await FinalPredictionContinuityRegression.RunAsync(path); if (r != 0) return r; }
if (From(18)) { var r = await DeterministicRerunRegression.RunAsync(path); if (r != 0) return r; }

if (From(18))
{
    var r = HistoricalMultiMatchProductionAcceptance.Run(path); if (r != 0) return r;
    return await FullPipelineRegressionRunner.RunAsync(path);
}
return 0;
