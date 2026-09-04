using HattrickAI.V5.Core;
using HattrickAI.V5.OfflineTests;

var startFrom = args.Length > 0 && args[0].StartsWith("c", StringComparison.OrdinalIgnoreCase) ? args[0].ToLowerInvariant() : "c1";
var path = args.Length > 1 ? args[1] : args.Length > 0 && !args[0].StartsWith("c", StringComparison.OrdinalIgnoreCase) ? args[0] : "TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json";

var webInputIntegrityRegression = WebInputIntegrityRegression.Run();
if (startFrom == "c1" && webInputIntegrityRegression != 0) return webInputIntegrityRegression;
var coreWebParityRegression = CoreWebParityRegression.Run(); if (startFrom == "c1" && coreWebParityRegression != 0) return coreWebParityRegression;
var c1M3ContinuityRegression = await M3M11EndToEndRegression.RunAsync(path); if (startFrom == "c1" && c1M3ContinuityRegression != 0) return c1M3ContinuityRegression;
var c2M4LegalFormationRegression = M4LegalFormationRegression.Run(); if (startFrom == "c2" && c2M4LegalFormationRegression != 0) return c2M4LegalFormationRegression;
var historicalCalibrationRegression = HistoricalCalibrationRegression.Run(); if (startFrom == "c1" && historicalCalibrationRegression != 0) return historicalCalibrationRegression;
var setPieceTakerCalibrationRegression = SetPieceTakerCalibrationRegression.Run(); if (startFrom == "c1" && setPieceTakerCalibrationRegression != 0) return setPieceTakerCalibrationRegression;
var specialtyInteractionRegression = SpecialtyInteractionRegression.Run(); if (startFrom == "c1" && specialtyInteractionRegression != 0) return specialtyInteractionRegression;
var tacticPaperMappingRegression = TacticPaperMappingRegression.Run(); if (startFrom == "c1" && tacticPaperMappingRegression != 0) return tacticPaperMappingRegression;
var longShotRegression = LongShotOpportunityRegression.Run(); if (startFrom == "c1" && longShotRegression != 0) return longShotRegression;
var m9EventRegression = M9EventGoalRegression.Run(); if (startFrom == "c1" && m9EventRegression != 0) return m9EventRegression;
var m5XiCandidatesRegression = await M5XICandidatesRegression.RunAsync(path); if (startFrom == "c1" && m5XiCandidatesRegression != 0) return m5XiCandidatesRegression;
var m6aCandidateEvaluationRegression = await M6ACandidateEvaluationRegression.RunAsync(path); if (startFrom == "c1" && m6aCandidateEvaluationRegression != 0) return m6aCandidateEvaluationRegression;
var c5M7RegionalRatingRegression = await M7RegionalRatingRegression.RunAsync(path); if (startFrom == "c1" && c5M7RegionalRatingRegression != 0) return c5M7RegionalRatingRegression;
var c6M72TacticalScenarioRegression = await M7_2TacticalScenarioRegression.RunAsync(path); if (startFrom == "c1" && c6M72TacticalScenarioRegression != 0) return c6M72TacticalScenarioRegression;
var c7M8ChanceModelRegression = await M8ChanceModelRegression.RunAsync(path); if (startFrom == "c1" && c7M8ChanceModelRegression != 0) return c7M8ChanceModelRegression;
var c8M9PredictionRegression = await M9PredictionRegression.RunAsync(path); if (startFrom == "c1" && c8M9PredictionRegression != 0) return c8M9PredictionRegression;
var c9Db1FormationCoverageRegression = await DB1FormationCoverageRegression.RunAsync(path); if (startFrom == "c1" && c9Db1FormationCoverageRegression != 0) return c9Db1FormationCoverageRegression;
var c10M10FormationCompetitionRegression = await M10FormationCompetitionRegression.RunAsync(path); if (startFrom == "c1" && c10M10FormationCompetitionRegression != 0) return c10M10FormationCompetitionRegression;
var c11M10ToM6BHandoffRegression = await M10ToM6BRankDrivenHandoffRegression.RunAsync(path); if (startFrom == "c1" && c11M10ToM6BHandoffRegression != 0) return c11M10ToM6BHandoffRegression;

var c12M6BRefinementRegression = await M6BRefinementRegression.RunAsync(path); if (c12M6BRefinementRegression != 0) return c12M6BRefinementRegression;
var c13Db2FormationCoverageRegression = await DB2FormationCoverageRegression.RunAsync(path); if (c13Db2FormationCoverageRegression != 0) return c13Db2FormationCoverageRegression;
var c14M11FinalistPoolRegression = await M11FinalistPoolRegression.RunAsync(path); if (c14M11FinalistPoolRegression != 0) return c14M11FinalistPoolRegression;
var c15M11FinalSelectionRegression = await M11FinalSelectionRegression.RunAsync(path); if (c15M11FinalSelectionRegression != 0) return c15M11FinalSelectionRegression;
var c16FinalPlanContinuityRegression = await FinalPlanContinuityRegression.RunAsync(path); if (c16FinalPlanContinuityRegression != 0) return c16FinalPlanContinuityRegression;
var c17FinalPredictionContinuityRegression = await FinalPredictionContinuityRegression.RunAsync(path); if (c17FinalPredictionContinuityRegression != 0) return c17FinalPredictionContinuityRegression;
var c18DeterministicRerunRegression = await DeterministicRerunRegression.RunAsync(path); if (c18DeterministicRerunRegression != 0) return c18DeterministicRerunRegression;
var historicalMultiMatchAcceptance = HistoricalMultiMatchProductionAcceptance.Run(path); if (historicalMultiMatchAcceptance != 0) return historicalMultiMatchAcceptance;
return await FullPipelineRegressionRunner.RunAsync(path);
