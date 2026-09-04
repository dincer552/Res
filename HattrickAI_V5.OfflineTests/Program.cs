using HattrickAI.V5.Core;
using HattrickAI.V5.OfflineTests;

var webInputIntegrityRegression = WebInputIntegrityRegression.Run();
if (webInputIntegrityRegression != 0) return webInputIntegrityRegression;

var coreWebParityRegression = CoreWebParityRegression.Run();
if (coreWebParityRegression != 0) return coreWebParityRegression;

var path = args.Length > 0 ? args[0] : "TestJSON/HattrickAI_V5_CHPP_FullOffline_2026-09-01.json";

var c1M3ContinuityRegression = await M3M11EndToEndRegression.RunAsync(path); if (c1M3ContinuityRegression != 0) return c1M3ContinuityRegression;
var c2M4LegalFormationRegression = M4LegalFormationRegression.Run(); if (c2M4LegalFormationRegression != 0) return c2M4LegalFormationRegression;
var historicalCalibrationRegression = HistoricalCalibrationRegression.Run(); if (historicalCalibrationRegression != 0) return historicalCalibrationRegression;
var setPieceTakerCalibrationRegression = SetPieceTakerCalibrationRegression.Run(); if (setPieceTakerCalibrationRegression != 0) return setPieceTakerCalibrationRegression;
var specialtyInteractionRegression = SpecialtyInteractionRegression.Run(); if (specialtyInteractionRegression != 0) return specialtyInteractionRegression;
var tacticPaperMappingRegression = TacticPaperMappingRegression.Run(); if (tacticPaperMappingRegression != 0) return tacticPaperMappingRegression;
var longShotRegression = LongShotOpportunityRegression.Run(); if (longShotRegression != 0) return longShotRegression;
var m9EventRegression = M9EventGoalRegression.Run(); if (m9EventRegression != 0) return m9EventRegression;
var m5XiCandidatesRegression = await M5XICandidatesRegression.RunAsync(path); if (m5XiCandidatesRegression != 0) return m5XiCandidatesRegression;
var m6aCandidateEvaluationRegression = await M6ACandidateEvaluationRegression.RunAsync(path); if (m6aCandidateEvaluationRegression != 0) return m6aCandidateEvaluationRegression;
var c5M7RegionalRatingRegression = await M7RegionalRatingRegression.RunAsync(path); if (c5M7RegionalRatingRegression != 0) return c5M7RegionalRatingRegression;
var c6M72TacticalScenarioRegression = await M7_2TacticalScenarioRegression.RunAsync(path); if (c6M72TacticalScenarioRegression != 0) return c6M72TacticalScenarioRegression;
var c7M8ChanceModelRegression = await M8ChanceModelRegression.RunAsync(path); if (c7M8ChanceModelRegression != 0) return c7M8ChanceModelRegression;
var c8M9PredictionRegression = await M9PredictionRegression.RunAsync(path); if (c8M9PredictionRegression != 0) return c8M9PredictionRegression;
var c9Db1FormationCoverageRegression = await DB1FormationCoverageRegression.RunAsync(path); if (c9Db1FormationCoverageRegression != 0) return c9Db1FormationCoverageRegression;
var c10M10FormationCompetitionRegression = await M10FormationCompetitionRegression.RunAsync(path); if (c10M10FormationCompetitionRegression != 0) return c10M10FormationCompetitionRegression;
var historicalMultiMatchAcceptance = HistoricalMultiMatchProductionAcceptance.Run(path); if (historicalMultiMatchAcceptance != 0) return historicalMultiMatchAcceptance;
return await FullPipelineRegressionRunner.RunAsync(path);
