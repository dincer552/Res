using HattrickAI.V5.Core;
using HattrickAI.V5.OfflineTests;

var longShotRegression = LongShotOpportunityRegression.Run();
if (longShotRegression != 0) return longShotRegression;

var m9EventRegression = M9EventGoalRegression.Run();
if (m9EventRegression != 0) return m9EventRegression;

var path = args.Length > 0 ? args[0] : "HattrickAI_V5_CHPP_FullOffline_2026-09-01T08-49-54-690Z(5).json";
return await FullPipelineRegressionRunner.RunAsync(path);
